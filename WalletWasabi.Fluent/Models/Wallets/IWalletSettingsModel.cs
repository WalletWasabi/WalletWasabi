using NBitcoin;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletSettingsModel
{
	bool PreferPsbtWorkflow { get; set; }

	bool AutoCoinjoin { get; set; }

	bool IsCoinjoinProfileSelected { get; set; }

	Money PlebStopThreshold { get; set; }

	int AnonScoreTarget { get; set; }

	bool RedCoinIsolation { get; set; }

	int FeeRateMedianTimeFrameHours { get; set; }

	void Save();
}
