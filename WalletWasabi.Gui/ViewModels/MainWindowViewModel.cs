using Avalonia.Controls;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Dialogs;
using AvalonStudio.Shell;
using NBitcoin;
using NBitcoin.Protocol;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.Controls.LockScreen;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.ViewModels
{
	public class MainWindowViewModel : ViewModelBase, IDisposable
	{
		private ModalDialogViewModelBase _modalDialog;
		private bool _canClose = true;
		private string _title = "Wasabi Wallet";
		private WindowState _windowState;
		private StatusBarViewModel _statusBar;
		private LockScreenViewModelBase _lockScreen;
		private volatile bool _disposedValue = false; // To detect redundant calls
		private Stack<LockScreenViewModelBase> _lockScreens;
		private bool _menuVisible;

		public MainWindowViewModel(Network network, UiConfig uiConfig, WalletManager walletManager, StatusBarViewModel statusBarViewModel, IShell shell)
		{
			Network = network;
			UiConfig = uiConfig;
			Shell = shell;
			WalletManager = walletManager;
			_lockScreens = new Stack<LockScreenViewModelBase>();

			_menuVisible = true;

			WindowState = uiConfig.WindowState;

			InitializeLockScreen();

			StatusBar = statusBarViewModel;

			DisplayWalletManager();
		}

		public string Title
		{
			get => _title;
			internal set => this.RaiseAndSetIfChanged(ref _title, value);
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
			private set => this.RaiseAndSetIfChanged(ref _lockScreen, value);
		}

		public bool MenuVisible
		{
			get => _menuVisible;
			set => this.RaiseAndSetIfChanged(ref _menuVisible, value);
		}
		public Network Network { get; }
		public UiConfig UiConfig { get; }
		public IShell Shell { get; }
		public WalletManager WalletManager { get; }
		public static MainWindowViewModel Instance { get; internal set; }

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

		public void PushLockScreen(LockScreenViewModelBase lockScreen)
		{
			if (LockScreen is { })
			{
				_lockScreens.Push(LockScreen);
			}

			MenuVisible = false;
			lockScreen.Initialize();
			LockScreen = lockScreen;
		}

		public void CloseLockScreen(LockScreenViewModelBase lockScreen)
		{
			if (lockScreen == LockScreen)
			{
				LockScreen?.Dispose();

				if (_lockScreens.Count > 0)
				{
					LockScreen = _lockScreens.Pop();
				}
				else
				{
					LockScreen = null;
					MenuVisible = true;
				}
			}
		}

		public void Initialize(NodesCollection connectedNodes, WasabiSynchronizer synchronizer)
		{
			StatusBar.Initialize(connectedNodes, synchronizer);

			if (Network != Network.Main)
			{
				Title += $" - {Network}";
			}
		}

		private void InitializeLockScreen()
		{
			UiConfig
				.WhenAnyValue(x => x.LockScreenActive)
				.Where(x => x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => PushLockScreen(UiConfig.LockScreenPinHash.Length == 0
						? (WasabiLockScreenViewModelBase)new SlideLockScreenViewModel()
						: new PinLockScreenViewModel()));
		}

		private void DisplayWalletManager()
		{
			var walletManagerViewModel = IoC.Get<WalletManagerViewModel>();
			Shell.AddDocument(walletManagerViewModel);

			var isAnyDesktopWalletAvailable = WalletManager.WalletDirectories.EnumerateWalletFiles().Any();

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
