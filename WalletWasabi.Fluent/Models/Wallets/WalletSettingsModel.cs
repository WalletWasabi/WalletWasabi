using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletSettingsModel : ReactiveObject, IWalletSettingsModel
{
	private readonly KeyManager _keyManager;
	private bool _isDirty;

	[AutoNotify] private bool _isNewWallet;
	[AutoNotify] private bool _autoCoinjoin;
	[AutoNotify] private bool _isCoinjoinProfileSelected;
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private Money _plebStopThreshold;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private int _feeRateMedianTimeFrameHours;

	public WalletSettingsModel(Wallet wallet, bool isNewWallet = false)
	{
		_keyManager = wallet.KeyManager;

		_isNewWallet = isNewWallet;
		_isDirty = isNewWallet;

		_autoCoinjoin = _keyManager.AutoCoinJoin;
		_isCoinjoinProfileSelected = _keyManager.IsCoinjoinProfileSelected;
		_preferPsbtWorkflow = _keyManager.PreferPsbtWorkflow;
		_plebStopThreshold = _keyManager.PlebStopThreshold ?? KeyManager.DefaultPlebStopThreshold;
		_anonScoreTarget = _keyManager.AnonScoreTarget;
		_redCoinIsolation = _keyManager.RedCoinIsolation;
		_feeRateMedianTimeFrameHours = _keyManager.FeeRateMedianTimeFrameHours;

		WalletName = _keyManager.WalletName;

		this.WhenAnyValue(
			x => x.AutoCoinjoin,
			x => x.IsCoinjoinProfileSelected,
			x => x.PreferPsbtWorkflow,
			x => x.PlebStopThreshold,
			x => x.AnonScoreTarget,
			x => x.RedCoinIsolation,
			x => x.FeeRateMedianTimeFrameHours)
			.Skip(1)
			.Do(_ => SetValues())
			.Subscribe();
	}

	public string WalletName { get; }

	public void Save()
	{
		if (_isDirty)
		{
			_keyManager.ToFile();

			_isDirty = false;
		}
	}

	private void SetValues()
	{
		_keyManager.AutoCoinJoin = AutoCoinjoin;
		_keyManager.IsCoinjoinProfileSelected = IsCoinjoinProfileSelected;
		_keyManager.PreferPsbtWorkflow = PreferPsbtWorkflow;
		_keyManager.PlebStopThreshold = PlebStopThreshold;
		_keyManager.SetAnonScoreTarget(AnonScoreTarget, false);
		_keyManager.RedCoinIsolation = RedCoinIsolation;
		_keyManager.SetFeeRateMedianTimeFrame(FeeRateMedianTimeFrameHours);

		_isDirty = true;
	}
}
