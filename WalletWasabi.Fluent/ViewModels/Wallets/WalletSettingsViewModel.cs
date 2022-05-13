using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private int _minAnonScoreTarget;
	[AutoNotify] private int _maxAnonScoreTarget;

	private Wallet _wallet;

	public WalletSettingsViewModel(WalletViewModelBase walletViewModelBase)
	{
		_wallet = walletViewModelBase.Wallet;
		Title = $"{_wallet.WalletName} - Wallet Settings";
		_preferPsbtWorkflow = _wallet.KeyManager.PreferPsbtWorkflow;
		_autoCoinJoin = _wallet.KeyManager.AutoCoinJoin;
		IsHardwareWallet = _wallet.KeyManager.IsHardwareWallet;
		IsWatchOnly = _wallet.KeyManager.IsWatchOnly;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To(new VerifyRecoveryWordsViewModel(_wallet)));

		_minAnonScoreTarget = _wallet.KeyManager.MinAnonScoreTarget;
		_maxAnonScoreTarget = _wallet.KeyManager.MaxAnonScoreTarget;

		CoinJoinProfiles = new CoinJoinProfilesViewModel(_wallet.KeyManager, isNewWallet: false);

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(value =>
			{
				_wallet.KeyManager.PreferPsbtWorkflow = value;
				_wallet.KeyManager.ToFile();
				walletViewModelBase.RaisePropertyChanged(nameof(walletViewModelBase.PreferPsbtWorkflow));
			});
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);
	}

	public CoinJoinProfilesViewModel CoinJoinProfiles { get; }

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public override sealed string Title { get; protected set; }

	public ICommand VerifyRecoveryWordsCommand { get; }
}
