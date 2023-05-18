using NBitcoin;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletSettingsModel
{
	string WalletName { get; }

	bool PreferPsbtWorkflow { get; set; }

	bool AutoCoinjoin { get; set; }

	bool IsCoinjoinProfileSelected { get; set; }

	Money PlebStopThreshold { get; set; }

	int AnonScoreTarget { get; set; }

	bool RedCoinIsolation { get; set; }

	int FeeRateMedianTimeFrameHours { get; set; }

	bool IsNewWallet { get; }

	void Save();
}
