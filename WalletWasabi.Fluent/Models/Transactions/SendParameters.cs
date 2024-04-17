using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Transactions;

public record SendParameters(
	Wallet Wallet,
	ICoinsView AvailableCoins,
	TransactionInfo? TransactionInfo = null)
{
	public static SendParameters Create(Wallet wallet) => new SendParameters(wallet, wallet.Coins);

	public static SendParameters CreateManual(Wallet wallet, IEnumerable<SmartCoin> coins) => new SendParameters(wallet, new CoinsView(coins));

	public decimal AvailableAmountBtc => AvailableCoins.TotalAmount().ToDecimal(NBitcoin.MoneyUnit.BTC);
}
