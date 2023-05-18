using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletSettingsModel : ReactiveObject, IWalletSettingsModel
{
	private readonly KeyManager _keyManager;
	[AutoNotify] private bool _isNewWallet;
	private bool _isDirty;

	public WalletSettingsModel(KeyManager keyManager, bool isNewWallet = false)
	{
		_keyManager = keyManager;

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

		_isNewWallet = isNewWallet;
		_isDirty = isNewWallet;

		WalletName = _keyManager.WalletName;
		AutoCoinjoin = _keyManager.AutoCoinJoin;
		IsCoinjoinProfileSelected = _keyManager.IsCoinjoinProfileSelected;
		PreferPsbtWorkflow = _keyManager.PreferPsbtWorkflow;
		PlebStopThreshold = _keyManager.PlebStopThreshold ?? KeyManager.DefaultPlebStopThreshold;
		AnonScoreTarget = _keyManager.AnonScoreTarget;
		RedCoinIsolation = _keyManager.RedCoinIsolation;
		FeeRateMedianTimeFrameHours = _keyManager.FeeRateMedianTimeFrameHours;
	}

	public string WalletName { get; }

	public bool AutoCoinjoin { get; set; }

	public bool IsCoinjoinProfileSelected { get; set; }

	public bool PreferPsbtWorkflow { get; set; }

	public Money PlebStopThreshold { get; set; }

	public int AnonScoreTarget { get; set; }

	public bool RedCoinIsolation { get; set; }

	public int FeeRateMedianTimeFrameHours { get; set; }

	public void Save()
	{
		if (_isDirty)
		{
			_keyManager.ToFile();

			if (IsNewWallet)
			{
				Services.WalletManager.AddWallet(_keyManager);
				IsNewWallet = false;
			}

			_isDirty = false;
		}
	}

	private void SetValues()
	{
		_keyManager.AutoCoinJoin = AutoCoinjoin;
		_keyManager.IsCoinjoinProfileSelected = IsCoinjoinProfileSelected;
		_keyManager.PreferPsbtWorkflow = PreferPsbtWorkflow;
		_keyManager.PlebStopThreshold = PlebStopThreshold;
		_keyManager.AnonScoreTarget = AnonScoreTarget;
		_keyManager.RedCoinIsolation = RedCoinIsolation;
		_keyManager.SetFeeRateMedianTimeFrame(FeeRateMedianTimeFrameHours);

		_isDirty = true;
	}
}
