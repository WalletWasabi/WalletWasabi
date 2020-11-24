using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Controls;
using Splat;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui
{
	public class MainWindow : MetroWindow
	{
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

			Closing += MainWindow_ClosingAsync;
			Activated += MainWindow_Activated;
			GotFocus += MainWindow_GotFocus;
			Opened += MainWindow_Opened;
			Initialized += MainWindow_Initialized;
			LayoutUpdated += MainWindow_LayoutUpdated;
		}

		private void MainWindow_LayoutUpdated(object? sender, EventArgs e)
		{
			Logger.LogInfo("MainWindow_LayoutUpdated");
		}

		private void MainWindow_Initialized(object? sender, EventArgs e)
		{
			Logger.LogInfo("MainWindow_Initialized");
		}

		private void MainWindow_Opened(object? sender, EventArgs e)
		{
			Logger.LogInfo("MainWindow_Opened");
		}

		private void MainWindow_GotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
		{
			Logger.LogInfo("MainWindow_GotFocus");
		}

		private void MainWindow_Activated(object? sender, EventArgs e)
		{
			Logger.LogInfo("MainWindow_Activated");
		}

		private Global Global { get; }

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private int _closingState;

		private async void MainWindow_ClosingAsync(object? sender, CancelEventArgs e)
		{
			e.Cancel = true;
			Hide();
		}

		private async Task ClosingAsync()
		{
			bool closeApplication = false;
			try
			{
				// Indicate -> do not add any more alices to the coinjoin.
				Global.WalletManager.SignalQuitPending(true);

				if (Global.WalletManager.AnyCoinJoinInProgress())
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
						if (Global.UiConfig is { }) // UiConfig not yet loaded.
						{
							Global.UiConfig.WindowState = WindowState;

							IDocumentTabViewModel? selectedDocument = IoC.Get<IShell>().SelectedDocument;
							Global.UiConfig.LastActiveTab = selectedDocument is null
								? nameof(HistoryTabViewModel)
								: selectedDocument.GetType().Name;

							Global.UiConfig.ToFile();
							Logger.LogInfo($"{nameof(Global.UiConfig)} is saved.");
						}

						Hide();
						var wm = IoC.Get<IShell>().Documents?.OfType<WalletManagerViewModel>().FirstOrDefault();
						if (wm is { })
						{
							wm.OnClose();
							Logger.LogInfo($"{nameof(WalletManagerViewModel)} closed.");
						}
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
					// Re-enable enqueuing coins.
					Global.WalletManager.SignalQuitPending(false);
				}
			}
		}
	}
}
