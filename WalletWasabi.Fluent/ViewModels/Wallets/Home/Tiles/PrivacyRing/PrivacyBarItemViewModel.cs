using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyBarItemViewModel : ViewModelBase
{
	public PrivacyBarItemViewModel(PrivacyBarViewModel parent, SmartCoin coin)
	{
		PrivacyLevel = coin.GetPrivacyLevel(parent.Wallet.AnonScoreTarget);
		Amount = coin.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC);
	}

	public PrivacyBarItemViewModel(PrivacyLevel privacyLevel, decimal amount)
	{
		PrivacyLevel = privacyLevel;
		Amount = amount;
	}

	public decimal Amount { get; }

	public PrivacyLevel PrivacyLevel { get; }
}
