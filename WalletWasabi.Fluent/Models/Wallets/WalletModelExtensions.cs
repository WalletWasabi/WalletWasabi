using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Models.Wallets;

public static class WalletModelExtensions
{
	public static Money TotalAmount(this IEnumerable<CoinModel> coins) => coins.Sum(x => x.Amount);

	public static decimal TotalBtcAmount(this IEnumerable<CoinModel> coins) => coins.TotalAmount().ToDecimal(MoneyUnit.BTC);

	public static IEnumerable<SmartCoin> GetSmartCoins(this IEnumerable<CoinModel> coins) =>
		coins.Select(x => x.GetSmartCoin()).ToList();
}
