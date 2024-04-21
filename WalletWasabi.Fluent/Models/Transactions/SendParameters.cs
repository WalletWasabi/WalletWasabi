using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
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

	public decimal AvailableAmountBtc => AvailableAmount.ToDecimal(MoneyUnit.BTC);

	public Money AvailableAmount => AvailableCoins.TotalAmount();

	public bool IsManual => AvailableCoins.TotalAmount() != Wallet.Coins.TotalAmount();

	public IEnumerable<(LabelsArray Labels, ICoinsView Coins)> GetPockets() => AvailableCoins.GetPockets(Wallet.AnonScoreTarget);
}
