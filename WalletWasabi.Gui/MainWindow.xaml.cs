using Avalonia;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;

namespace WalletWasabi.Gui
{
	public class MainWindow : MetroWindow
	{
		private const int ClosingStateNotClosing = 0;
		private const int ClosingStateInProgress = 1;
		private const int ClosingStateClosed = 2;

		public MainWindow()
		{
			Global = Locator.Current.GetService<Global>();
			TerminateService = Locator.Current.GetService<TerminateService>();

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
		}

		private Global Global { get; }
		public TerminateService TerminateService { get; }

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private int _closingState;
		private bool IsClosed => _closingState == ClosingStateClosed;

		private async void MainWindow_ClosingAsync(object? sender, CancelEventArgs e)
		{
			try
			{
				e.Cancel = true;
				switch (Interlocked.CompareExchange(ref _closingState, ClosingStateInProgress, ClosingStateNotClosing))
				{
					case ClosingStateNotClosing:
						// We only try to dequeue with the UI dialog if Termination was not requested, only the X button was clicked.
						await ClosingAsync(tryToDequeue: !TerminateService.IsTerminateRequested);
						break;

					case ClosingStateInProgress:
						// Still closing, cancel the progress.
						return;

					case ClosingStateClosed:
						e.Cancel = false;
						return; // Can close the window.
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		/// <summary>
		/// This will try to close the main window of the application.
		/// </summary>
		/// <param name="tryToDequeue">
		/// If true then the user will get a dialog where a status of the dequeuing process is shown and the window will be kept open until it successfully finishes.
		/// If false then the dequeuing process will be handled later at the disposal of the business logic.
		/// </param>/// <returns></returns>
		public async Task ClosingAsync(bool tryToDequeue)
		{
			if (IsClosed)
			{
				return;
			}

			if (Dispatcher.UIThread?.CheckAccess() is false)
			{
				// We are not on the UI thread, let's synchronize.
				await Dispatcher.UIThread.InvokeAsync(async () =>
				{
					// Safety check if it was closed meanwhile synchronization.
					if (!IsClosed)
					{
						await ClosingAsync(tryToDequeue).ConfigureAwait(false);
					}
				}).ConfigureAwait(false);
				return;
			}

			// We are on the UI Thread now, do not use ConfigureAwait(false) after this line.

			bool closeApplication = true;
			try
			{
				// Indicate -> do not add any more alices to the coinjoin.
				Global.WalletManager.SignalQuitPending(true);
				if (tryToDequeue && Global.WalletManager.AnyCoinJoinInProgress())
				{
					var dialog = new CannotCloseDialogViewModel();

					// Start the deque process with a dialog.
					closeApplication = await MainWindowViewModel.Instance.ShowDialogAsync(dialog);
				}

				if (!closeApplication)
				{
					//The user aborted the close, let's go to finally.
					return;
				}

				try
				{
					SaveUiConfig();

					Hide();

					if (IoC.Get<IShell>().Documents is { } docs)
					{
						foreach (var doc in docs.OfType<WasabiDocumentTabViewModel>())
						{
							doc.OnClose();
							Logger.LogInfo($"ViewModel {doc.Title} was closed.");
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
				}

				// Now we can close the app.
				Interlocked.Exchange(ref _closingState, ClosingStateClosed);

				// Don't call the MainWindow_ClosingAsync again.
				Closing -= MainWindow_ClosingAsync;
				Close();
			}
			catch (Exception ex)
			{
				// Something happened back to starting point.
				Interlocked.Exchange(ref _closingState, ClosingStateNotClosing);
				Logger.LogWarning(ex);
			}
			finally
			{
				if (!closeApplication)
				{
					// We are not closing the application for some reason.
					Interlocked.Exchange(ref _closingState, ClosingStateNotClosing);
					// Re-enable enqueuing coins.
					Global.WalletManager.SignalQuitPending(false);
				}
			}
		}

		private void SaveUiConfig()
		{
			if (Global.UiConfig is { } uiConfig)
			{
				uiConfig.WindowState = WindowState;

				IDocumentTabViewModel? selectedDocument = IoC.Get<IShell>().SelectedDocument;
				uiConfig.LastActiveTab = selectedDocument is null
					? nameof(HistoryTabViewModel)
					: selectedDocument.GetType().Name;

				uiConfig.ToFile();
				Logger.LogInfo($"{nameof(uiConfig)} is saved.");
			}
		}
	}
}
