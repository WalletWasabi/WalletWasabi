using Avalonia.Controls;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Dialogs;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.LockScreen;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.ViewModels
{
	public class MainWindowViewModel : ViewModelBase, IDisposable
	{
		private volatile bool _disposedValue = false;
		private ModalDialogViewModelBase _modalDialog;
		private bool _canClose = true;
		private string _title = "Wasabi Wallet";
		private double _height;
		private double _width;
		private WindowState _windowState;
		private StatusBarViewModel _statusBar;
		private LockScreenViewModelBase _lockScreen;

		public MainWindowViewModel()
		{
			var global = Locator.Current.GetService<Global>();

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var uiConfig = global.UiConfig;

				Width = uiConfig.Width;
				Height = uiConfig.Height;
				WindowState = uiConfig.WindowState;
			}
			else
			{
				WindowState = WindowState.Maximized;
			}

			InitializeLockScreen(global.UiConfig);

			StatusBar = new StatusBarViewModel();

			DisplayWalletManager();
		}

		public string Title
		{
			get => _title;
			internal set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public double Height
		{
			get => _height;
			set => this.RaiseAndSetIfChanged(ref _height, value);
		}

		public double Width
		{
			get => _width;
			set => this.RaiseAndSetIfChanged(ref _width, value);
		}

		public WindowState WindowState
		{
			get => _windowState;
			set => this.RaiseAndSetIfChanged(ref _windowState, value);
		}

		public StatusBarViewModel StatusBar
		{
			get => _statusBar;
			set => this.RaiseAndSetIfChanged(ref _statusBar, value);
		}

		public LockScreenViewModelBase LockScreen
		{
			get => _lockScreen;
			set => this.RaiseAndSetIfChanged(ref _lockScreen, value);
		}

		public ModalDialogViewModelBase ModalDialog
		{
			get => _modalDialog;
			private set => this.RaiseAndSetIfChanged(ref _modalDialog, value);
		}

		public bool CanClose
		{
			get => _canClose;
			set => this.RaiseAndSetIfChanged(ref _canClose, value);
		}

		public static MainWindowViewModel Instance { get; internal set; }

		public ReactiveCommand<Unit, Unit> LockScreenCommand { get; }

		public void Initialize()
		{
			var global = Locator.Current.GetService<Global>();

			if (global.Nodes != null)
			{
				StatusBar.Initialize(global.Nodes.ConnectedNodes, global.Synchronizer);
			}

			if (global.Network != Network.Main)
			{
				Instance.Title += $" - {global.Network}";
			}
		}

		private void InitializeLockScreen(UiConfig uiConfig)
		{
			uiConfig
				.WhenAnyValue(x => x.LockScreenActive)
				.Where(x => x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					LockScreen?.Dispose();

					LockScreen = uiConfig.LockScreenPinHash.Length == 0 ?
						(LockScreenViewModelBase)new SlideLockScreenViewModel() :
						new PinLockScreenViewModel();
				});
		}

		private void DisplayWalletManager()
		{
			var walletManagerViewModel = IoC.Get<WalletManagerViewModel>();
			IoC.Get<IShell>().AddDocument(walletManagerViewModel);

			var global = Locator.Current.GetService<Global>();

			var isAnyDesktopWalletAvailable = Directory.Exists(global.WalletsDir) && Directory.EnumerateFiles(global.WalletsDir).Any();

			if (isAnyDesktopWalletAvailable)
			{
				walletManagerViewModel.SelectLoadWallet();
			}
			else
			{
				walletManagerViewModel.SelectGenerateWallet();
			}
		}

		public async Task<bool> ShowDialogAsync(ModalDialogViewModelBase dialog)
		{
			ModalDialog = dialog;

			bool res = await ModalDialog.ShowDialogAsync();

			ModalDialog = null;

			return res;
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					StatusBar?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
