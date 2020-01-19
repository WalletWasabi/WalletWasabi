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
		private ModalDialogViewModelBase _modalDialog;
		private bool _canClose = true;

		private string _title = "Wasabi Wallet";

		public string Title
		{
			get => _title;
			internal set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		private double _height;

		public double Height
		{
			get => _height;
			set => this.RaiseAndSetIfChanged(ref _height, value);
		}

		private double _width;

		public double Width
		{
			get => _width;
			set => this.RaiseAndSetIfChanged(ref _width, value);
		}

		private WindowState _windowState;

		public WindowState WindowState
		{
			get => _windowState;
			set => this.RaiseAndSetIfChanged(ref _windowState, value);
		}

		private StatusBarViewModel _statusBar;

		public StatusBarViewModel StatusBar
		{
			get => _statusBar;
			set => this.RaiseAndSetIfChanged(ref _statusBar, value);
		}

		private LockScreenViewModelBase _lockScreen;

		public LockScreenViewModelBase LockScreen
		{
			get => _lockScreen;
			set => this.RaiseAndSetIfChanged(ref _lockScreen, value);
		}

		public ReactiveCommand<Unit, Unit> LockScreenCommand { get; }

		public MainWindowViewModel()
		{
			Shell = IoC.Get<IShell>();

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

		public void Initialize()
		{
			var global = Locator.Current.GetService<Global>();

			StatusBar.Initialize(global.Nodes.ConnectedNodes, global.Synchronizer);

			if (global.Network != Network.Main)
			{
				MainWindowViewModel.Instance.Title += $" - {global.Network}";
			}
		}

		private void InitializeLockScreen(UiConfig uiConfig)
		{
			uiConfig
				.WhenAnyValue(x => x.LockScreenActive)
				.Where(x => x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => CheckLockScreenType(uiConfig.LockScreenPinHash));
		}

		private void CheckLockScreenType(string currentHash)
		{
			LockScreen?.Dispose();

			if (currentHash.Length == 0)
			{
				LockScreen = new SlideLockScreenViewModel();
			}
			else
			{
				LockScreen = new PinLockScreenViewModel();
			}
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

		public IShell Shell { get; }

		public static MainWindowViewModel Instance { get; internal set; }

		public async Task<bool> ShowDialogAsync(ModalDialogViewModelBase dialog)
		{
			ModalDialog = dialog;

			bool res = await ModalDialog.ShowDialogAsync();

			ModalDialog = null;

			return res;
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

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

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
