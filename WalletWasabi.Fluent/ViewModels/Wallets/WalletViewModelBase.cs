using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public abstract class WalletViewModelBase : NavBarItemViewModel, IComparable<WalletViewModelBase>, IDisposable
	{
		private string _titleTip;
		private WalletState _walletState;
		private CompositeDisposable? _disposables;
		private bool _disposedValue;

		protected WalletViewModelBase(NavigationStateViewModel navigationState, Wallet wallet) : base(navigationState, NavigationTarget.HomeScreen)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);

			_disposables = new CompositeDisposable();

			Title = WalletName;
			var isHardware = Wallet.KeyManager.IsHardwareWallet;
			var isWatch = Wallet.KeyManager.IsWatchOnly;
			_titleTip = isHardware ? "Hardware Wallet" : isWatch ? "Watch Only Wallet" : "Hot Wallet";

			WalletState = wallet.State;

			Observable.FromEventPattern<WalletState>(wallet, nameof(wallet.StateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => WalletState = x.EventArgs)
				.DisposeWith(_disposables);
		}

		public WalletState WalletState
		{
			get => _walletState;
			private set => this.RaiseAndSetIfChanged(ref _walletState, value);
		}

		public Wallet Wallet { get; }

		public string TitleTip
		{
			get => _titleTip;
			set => this.RaiseAndSetIfChanged(ref _titleTip, value);
		}

		public string WalletName => Wallet.WalletName;

		public int CompareTo([AllowNull] WalletViewModelBase other)
		{
			if (WalletState != other!.WalletState)
			{
				if (WalletState == WalletState.Started || other.WalletState == WalletState.Started)
				{
					return other.WalletState.CompareTo(WalletState);
				}
			}

			return Title.CompareTo(other!.Title);
		}

		public override string ToString() => WalletName;

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_disposables?.Dispose();
					_disposables = null;
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
