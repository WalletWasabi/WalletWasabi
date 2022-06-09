using ReactiveUI;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

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

		this.WhenAnyValue(x => x.IsCoinJoining)
			.Skip(1)
			.Subscribe(x =>
			{
				MainViewModel.Instance.InvalidateIsCoinJoinActive();
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
		var walletType = WalletHelpers.GetType(Wallet.KeyManager);

		var baseResourceName = walletType switch
		{
			WalletType.Coldcard => "coldcard_24",
			WalletType.Trezor => "trezor_24",
			WalletType.Ledger => "ledger_24",
			_ => "wallet_24"
		};

		IconName = $"nav_{baseResourceName}_regular";
		IconNameFocused = $"nav_{baseResourceName}_filled";
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable.FromEventPattern<WalletState>(Wallet, nameof(Wallet.StateChanged))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => WalletState = x.EventArgs)
			.DisposeWith(disposables);
	}

	public int CompareTo(WalletViewModelBase? other)
	{
		if (other is null)
		{
			return -1;
		}

		var result = other.IsLoggedIn.CompareTo(IsLoggedIn);

		if (result == 0)
		{
			result = string.Compare(Title, other.Title, StringComparison.Ordinal);
		}

		return result;
	}

	public override string ToString() => WalletName;
}
