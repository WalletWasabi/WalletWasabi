using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public static class TransactionExtensions
{
	public static IEnumerable<Feature> Features(this ITransaction transaction) => transaction.Outputs.SelectMany(x => x.Features).Distinct();
	public static Money OutputAmount(this ITransaction transaction) => transaction.Outputs.Sum(x => x.Amount);
	public static Money? InputAmount(this ITransaction transaction) => transaction.Inputs.OfType<UnknownInputViewModel>().Any() ? null : transaction.Inputs.Cast<KnownInputViewModel>().Sum(x => x.Amount);

	public static Money? Fee(this ITransaction transaction)
	{
		if (InputAmount(transaction) is { } amount)
		{
			return amount - OutputAmount(transaction);
		}

		return null;
	}

	public static FeeRate? FeeRate(this ITransaction transaction)
	{
		var fee = Fee(transaction);
		if (fee is not null)
		{
			var virtualSize = (decimal) (fee.Satoshi / transaction.VirtualSize);
			var money = new Money(virtualSize, MoneyUnit.Satoshi);
			return new FeeRate(money);
		}

		return null;
	}
}
