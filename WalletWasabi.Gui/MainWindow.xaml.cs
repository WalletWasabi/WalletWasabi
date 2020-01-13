using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Native;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Theme;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Controls;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.ComponentModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui
{
	public class MainWindow : MetroWindow
	{
		public bool IsQuitPending { get; private set; }

		public MainWindow()
		{
			Global = Locator.Current.GetService<Global>();

			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif

			var notificationManager = new WindowNotificationManager(this)
			{
				Position = NotificationPosition.BottomRight,
				MaxItems = 4,
				Margin = new Thickness(0, 0, 15, 40)
			};

			Locator.CurrentMutable.RegisterConstant<INotificationManager>(notificationManager);
		}

		private Global Global { get; }

		private void InitializeComponent()
		{
			Closing += MainWindow_ClosingAsync;
			AvaloniaXamlLoader.Load(this);
			DisplayWalletManager();

			var uiConfigFilePath = Path.Combine(Global.DataDir, "UiConfig.json");
			var uiConfig = new UiConfig(uiConfigFilePath);
			uiConfig.LoadOrCreateDefaultFileAsync()
				.ToObservable(RxApp.TaskpoolScheduler)
				.Take(1)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					try
					{
						Global.InitializeUiConfig(uiConfig);
						Logger.LogInfo($"{nameof(Global.UiConfig)} is successfully initialized.");

						if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
						{
							MainWindowViewModel.Instance.Width = uiConfig.Width;
							MainWindowViewModel.Instance.Height = uiConfig.Height;
							MainWindowViewModel.Instance.WindowState = uiConfig.WindowState;
						}
						else
						{
							MainWindowViewModel.Instance.WindowState = WindowState.Maximized;
						}

						MainWindowViewModel.Instance.LockScreen.Initialize();
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				},
				onError: ex => Logger.LogError(ex));
		}

		private int _closingState;

		private async void MainWindow_ClosingAsync(object sender, CancelEventArgs e)
		{
			try
			{
				e.Cancel = true;
				switch (Interlocked.CompareExchange(ref _closingState, 1, 0))
				{
					case 0:
						await ClosingAsync();
						break;

					case 1:
						// still closing cancel the progress
						return;

					case 2:
						e.Cancel = false;
						return; //can close the window
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		private async Task ClosingAsync()
		{
			bool closeApplication = false;
			try
			{
				if (Global.ChaumianClient != null)
				{
					Global.ChaumianClient.IsQuitPending = true; // indicate -> do not add any more alices to the coinjoin
				}

				if (!MainWindowViewModel.Instance.CanClose)
				{
					var dialog = new CannotCloseDialogViewModel();

					closeApplication = await MainWindowViewModel.Instance.ShowDialogAsync(dialog); // start the deque process with a dialog
				}
				else
				{
					closeApplication = true;
				}

				if (closeApplication)
				{
					try
					{
						if (Global.UiConfig != null) // UiConfig not yet loaded.
						{
							Global.UiConfig.WindowState = WindowState;
							Global.UiConfig.Width = Width;
							Global.UiConfig.Height = Height;
							await Global.UiConfig.ToFileAsync();
							Logger.LogInfo($"{nameof(Global.UiConfig)} is saved.");
						}

						Hide();
						var wm = IoC.Get<IShell>().Documents?.OfType<WalletManagerViewModel>().FirstOrDefault();
						if (wm is { })
						{
							wm.OnClose();
							Logger.LogInfo($"{nameof(WalletManagerViewModel)} closed.");
						}

						await Global.DisposeAsync();
					}
					catch (Exception ex)
					{
						Logger.LogWarning(ex);
					}

					Interlocked.Exchange(ref _closingState, 2); //now we can close the app
					Close(); // start the closing process. Will call MainWindow_ClosingAsync again!
				}
				//let's go to finally
			}
			catch (Exception ex)
			{
				Interlocked.Exchange(ref _closingState, 0); //something happened back to starting point
				Logger.LogWarning(ex);
			}
			finally
			{
				if (!closeApplication) //we are not closing the application for some reason
				{
					Interlocked.Exchange(ref _closingState, 0);
					if (Global.ChaumianClient != null)
					{
						Global.ChaumianClient.IsQuitPending = false; //re-enable enqueuing coins
					}
				}
			}
		}

		private void DisplayWalletManager()
		{
			var walletManagerViewModel = IoC.Get<WalletManagerViewModel>();
			IoC.Get<IShell>().AddDocument(walletManagerViewModel);

			var isAnyDesktopWalletAvailable = Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any();

			if (isAnyDesktopWalletAvailable)
			{
				walletManagerViewModel.SelectLoadWallet();
			}
			else
			{
				walletManagerViewModel.SelectGenerateWallet();
			}
		}
	}
}
