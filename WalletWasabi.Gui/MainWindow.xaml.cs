using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Theme;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Controls;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Hwi;

namespace WalletWasabi.Gui
{
	public class MainWindow : MetroWindow
	{
		public bool IsQuitPending { get; private set; }

		public MainWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				HasSystemDecorations = true;

				// This will need implementing properly once this is supported by avalonia itself.
				var color = (ColorTheme.CurrentTheme.Background as SolidColorBrush).Color;
				(PlatformImpl as Avalonia.Native.WindowImpl).SetTitleBarColor(color);
			}
		}

		private void InitializeComponent()
		{
			Activated += OnActivated;
			Closing += MainWindow_ClosingAsync;
			AvaloniaXamlLoader.Load(this);
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
				Logging.Logger.LogError<MainWindow>(ex);
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
							Logging.Logger.LogInfo<UiConfig>("UiConfig is saved.");
						}
					}
					catch (Exception ex)
					{
						Logging.Logger.LogWarning<MainWindow>(ex);
					}
					Interlocked.Exchange(ref _closingState, 2); //now we can close the app

					await Global.DisposeAsync();

					Close(); // start the closing process. Will call MainWindow_ClosingAsync again!
				}
				//let's go to finally
			}
			catch (Exception ex)
			{
				Interlocked.Exchange(ref _closingState, 0); //something happened back to starting point
				Logging.Logger.LogWarning<MainWindow>(ex);
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

#pragma warning disable IDE1006 // Naming Styles

		private async void OnActivated(object sender, EventArgs e)
#pragma warning restore IDE1006 // Naming Styles
		{
			try
			{
				Activated -= OnActivated;

				var uiConfigFilePath = Path.Combine(Global.DataDir, "UiConfig.json");
				var uiConfig = new UiConfig(uiConfigFilePath);
				await uiConfig.LoadOrCreateDefaultFileAsync();
				Global.InitializeUiConfig(uiConfig);
				Logging.Logger.LogInfo<UiConfig>("UiConfig is successfully initialized.");

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					MainWindowViewModel.Instance.Width = (double)uiConfig.Width;
					MainWindowViewModel.Instance.Height = (double)uiConfig.Height;
					MainWindowViewModel.Instance.WindowState = (WindowState)uiConfig.WindowState;
				}
				else
				{
					MainWindowViewModel.Instance.WindowState = WindowState.Maximized;
				}
				DisplayWalletManager();
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<MainWindow>(ex);
			}
		}

		private void DisplayWalletManager()
		{
			var walletManagerViewModel = new WalletManagerViewModel();
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
