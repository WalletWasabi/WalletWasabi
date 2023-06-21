using NBitcoin;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletSettingsModel
{
	string WalletName { get; }

	WalletType WalletType { get; }

	bool IsNewWallet { get; }

	bool PreferPsbtWorkflow { get; set; }

	bool AutoCoinjoin { get; set; }

	bool IsCoinjoinProfileSelected { get; set; }

	Money PlebStopThreshold { get; set; }

	int AnonScoreTarget { get; set; }

	bool RedCoinIsolation { get; set; }

	CoinjoinSkipFactors CoinjoinSkipFactors { get; set; }

	int FeeRateMedianTimeFrameHours { get; set; }

	void Save();
}
