using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent;

public interface ITransaction
{
	public IObservable<int> Confirmations { get; }
	public IEnumerable<InputViewModel> Inputs { get; }
	public IEnumerable<OutputViewModel> Outputs { get; }
	public DateTimeOffset Timestamp { get; }
	public int IncludedInBlock { get; }
	public Money OutputAmount => this.OutputAmount();
	public Money? InputAmount => this.InputAmount();
	public Money Amount { get; }
	string Id { get; }
	public double Size { get; }
	public int Version { get; }
	public long BlockTime { get; }
	public double Weight { get; }
	public double VirtualSize { get; }
	IEnumerable<string> Labels { get; }
}

public static class TransactionExtensions
{
	public static Money OutputAmount(this ITransaction transaction) => transaction.Outputs.Sum(x => x.Amount);
	public static Money? InputAmount(this ITransaction transaction) => transaction.Inputs.Cast<UnknownInputViewModel>().Any() ? null : transaction.Inputs.Cast<KnownInputViewModel>().Sum(x => x.Amount);

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
