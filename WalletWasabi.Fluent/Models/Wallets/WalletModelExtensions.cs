using DynamicData;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Models.Wallets;

public static class WalletModelExtensions
{
	public static IObservable<IChangeSet<IAddress, string>> UnusedAddresses(this IWalletModel wallet) =>
		wallet.Addresses
			.ToObservableChangeSet(x => x.Text)
			.AutoRefresh(x => x.IsUsed)
			.Filter(x => !x.IsUsed);

	public static Money TotalAmount(this IEnumerable<ICoinModel> coins) => coins.Sum(x => x.Amount);

	public static decimal TotalBtcAmount(this IEnumerable<ICoinModel> coins) => coins.TotalAmount().ToDecimal(MoneyUnit.BTC);

	public static IEnumerable<SmartCoin> GetSmartCoins(this IEnumerable<ICoinModel> coins) =>
		coins.Select(x => x.GetSmartCoin()).ToList();
}
