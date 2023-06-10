using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public class WalletSettingsModel : IWalletSettingsModel
{
	private readonly Wallet _wallet;

	public WalletSettingsModel(Wallet wallet)
	{
		_wallet = wallet;

		AutoCoinjoin = _wallet.KeyManager.AutoCoinJoin;
		IsCoinjoinProfileSelected = _wallet.KeyManager.IsCoinjoinProfileSelected;
		PreferPsbtWorkflow = _wallet.KeyManager.PreferPsbtWorkflow;
		PlebStopThreshold = wallet.KeyManager.PlebStopThreshold ?? KeyManager.DefaultPlebStopThreshold;
		AnonScoreTarget = _wallet.KeyManager.AnonScoreTarget;
		RedCoinIsolation = _wallet.KeyManager.RedCoinIsolation;
		FeeRateMedianTimeFrameHours = _wallet.KeyManager.FeeRateMedianTimeFrameHours;
	}

	public bool AutoCoinjoin { get; set; }

	public bool IsCoinjoinProfileSelected { get; set; }

	public bool PreferPsbtWorkflow { get; set; }

	public Money PlebStopThreshold { get; set; }

	public int AnonScoreTarget { get; set; }

	public bool RedCoinIsolation { get; set; }

	public int FeeRateMedianTimeFrameHours { get; set; }

	public void Save()
	{
		_wallet.KeyManager.AutoCoinJoin = AutoCoinjoin;
		_wallet.KeyManager.IsCoinjoinProfileSelected = IsCoinjoinProfileSelected;
		_wallet.KeyManager.PreferPsbtWorkflow = PreferPsbtWorkflow;
		_wallet.KeyManager.PlebStopThreshold = PlebStopThreshold;
		_wallet.KeyManager.SetAnonScoreTarget(AnonScoreTarget, false);
		_wallet.KeyManager.RedCoinIsolation = RedCoinIsolation;
		_wallet.KeyManager.SetFeeRateMedianTimeFrame(FeeRateMedianTimeFrameHours, false);

		_wallet.KeyManager.ToFile();
	}
}
