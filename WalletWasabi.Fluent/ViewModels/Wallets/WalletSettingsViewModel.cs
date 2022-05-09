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
	private readonly WalletViewModelBase _walletViewModelBase;

	public WalletSettingsViewModel(WalletViewModelBase walletViewModelBase)
	{
		_wallet = walletViewModelBase.Wallet;
		Title = $"{_wallet.WalletName} - Wallet Settings";
		_preferPsbtWorkflow = _wallet.KeyManager.PreferPsbtWorkflow;
		_autoCoinJoin = _wallet.KeyManager.AutoCoinJoin;
		IsHardwareWallet = _wallet.KeyManager.IsHardwareWallet;
		IsWatchOnly = _wallet.KeyManager.IsWatchOnly;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = ReactiveCommand.Create(OnNext);

		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To(new VerifyRecoveryWordsViewModel(_wallet)));

		_minAnonScoreTarget = _wallet.KeyManager.MinAnonScoreTarget;
		_maxAnonScoreTarget = _wallet.KeyManager.MaxAnonScoreTarget;

		CoinJoinProfiles = new CoinJoinProfilesViewModel(_wallet.KeyManager, false);
		_walletViewModelBase = walletViewModelBase;
	}

	private void OnNext()
	{
		var selected = CoinJoinProfiles.SelectedProfile ?? CoinJoinProfiles.SelectedManualProfile;
		if (selected is { })
		{
			MinAnonScoreTarget = selected.MinAnonScoreTarget;
			MaxAnonScoreTarget = selected.MaxAnonScoreTarget;
			AutoCoinJoin = selected.AutoCoinjoin;

			_wallet.KeyManager.PreferPsbtWorkflow = PreferPsbtWorkflow;
			_wallet.KeyManager.SetAnonScoreTargets(MinAnonScoreTarget, MaxAnonScoreTarget);
			_wallet.KeyManager.AutoCoinJoin = AutoCoinJoin;
			_wallet.KeyManager.PlebStopThreshold = CoinJoinProfiles.PlebStopThreshold;

			_walletViewModelBase.RaisePropertyChanged(nameof(_walletViewModelBase.PreferPsbtWorkflow));
		}

		_wallet.KeyManager.ToFile();
		Navigate().Clear();
	}

	public CoinJoinProfilesViewModel CoinJoinProfiles { get; }

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public override sealed string Title { get; protected set; }

	public ICommand VerifyRecoveryWordsCommand { get; }
}
