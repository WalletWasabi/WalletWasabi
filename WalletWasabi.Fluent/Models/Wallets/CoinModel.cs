using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class CoinModel : ReactiveObject, ICoinModel
{
	[AutoNotify] private PrivacyLevel _privacyLevel;

	public CoinModel(Wallet wallet, SmartCoin coin)
	{
		_privacyLevel = coin.GetPrivacyLevel(wallet.AnonScoreTarget);
		Amount = coin.Amount;
	}

	public Money Amount { get; }
}
