using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Theme;
using AvalonStudio.Shell;
using AvalonStudio.Shell.Controls;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui
{
	public class MainWindow : MetroWindow, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		public bool IsQuitPending { get; private set; }

		public MainWindow()
		{
			Disposables = new CompositeDisposable();

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

		private long _closingState;

		private void MainWindow_ClosingAsync(object sender, CancelEventArgs e)
		{
			e.Cancel = true;
			switch (Interlocked.Read(ref _closingState))
			{
				case 0:
					Interlocked.Increment(ref _closingState);
					ClosingAsync().DisposeWith(Disposables);
					break;

				case 1:
					// still closing cancel the progress
					return;

				case 2:
					e.Cancel = false;
					return; //can close the window
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
					using (var dialog = new CannotCloseDialogViewModel().DisposeWith(Disposables))
					{
						closeApplication = await MainWindowViewModel.Instance.ShowDialogAsync(dialog); // start the deque process with a dialog
					}
				}
				else
				{
					closeApplication = true;
				}

				if (closeApplication)
				{
					try
					{
						Global.UiConfig.WindowState = WindowState;
						Global.UiConfig.Width = Width;
						Global.UiConfig.Height = Height;
						await Global.UiConfig.ToFileAsync();
						Logging.Logger.LogInfo<UiConfig>("UiConfig is saved.");
					}
					catch (Exception ex)
					{
						Logging.Logger.LogWarning<MainWindow>(ex);
					}
					Interlocked.Exchange(ref _closingState, 2); //now we can close the app
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
			var isAnyWalletAvailable = Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any();

			var walletManagerViewModel = new WalletManagerViewModel().DisposeWith(Disposables);
			IoC.Get<IShell>().AddDocument(walletManagerViewModel);

			if (isAnyWalletAvailable)
			{
				walletManagerViewModel.SelectLoadWallet();
			}
			else
			{
				walletManagerViewModel.SelectGenerateWallet();
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
