using ReactiveUI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public abstract partial class WalletViewModelBase : NavBarItemViewModel, IComparable<WalletViewModelBase>
	{
		[AutoNotify] private string _titleTip;
		[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isLoading;
		[AutoNotify(SetterModifier = AccessModifier.Protected)] private bool _isCoinJoining;
		[AutoNotify(SetterModifier = AccessModifier.Protected)] private string? _statusText;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private WalletState _walletState;

		private string _title;

		protected WalletViewModelBase(Wallet wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);

			_title = WalletName;
			var isHardware = Wallet.KeyManager.IsHardwareWallet;
			var isWatch = Wallet.KeyManager.IsWatchOnly;
			_titleTip = isHardware ? "Hardware Wallet" : isWatch ? "Watch Only Wallet" : "Hot Wallet";

			WalletState = wallet.State;

			OpenCommand = ReactiveCommand.Create(() => Navigate().To(this, NavigationMode.Clear));

			SetIcon();

			this.WhenAnyValue(x => x.IsLoading, x => x.IsCoinJoining)
				.Subscribe(tup =>
				{
					var (isLoading, isCoinJoining) = tup;

					if (isLoading)
					{
						StatusText = "Loading";
					}
					else if (isCoinJoining)
					{
						StatusText = "Coinjoining";
					}
					else
					{
						StatusText = null;
					}
				});
		}

		public override string Title
		{
			get => _title;
			protected set => this.RaiseAndSetIfChanged(ref _title, value);
		}

		public Wallet Wallet { get; }

		public string WalletName => Wallet.WalletName;

		public bool IsLoggedIn => Wallet.IsLoggedIn;

		public bool PreferPsbtWorkflow => Wallet.KeyManager.PreferPsbtWorkflow;

		private void SetIcon()
		{
			if (Wallet.KeyManager.Icon is { } iconString)
			{
				IconName = iconString;
			}
			else if (Wallet.KeyManager.IsHardwareWallet)
			{
				IconName = "General";
			}
			else
			{
				IconName = "default_wallet_icon";
			}
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => WalletState = x.EventArgs)
				.DisposeWith(disposables);
		}

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
	}
}
