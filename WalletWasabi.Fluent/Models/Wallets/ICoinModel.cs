using NBitcoin;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface ICoinModel
{
	Money Amount { get; }
	PrivacyLevel PrivacyLevel { get; }
}
