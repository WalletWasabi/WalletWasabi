using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Fluent;

public interface ITransactionDetailsViewModel
{
	public decimal FeeRate { get; }
	public Money Fee { get; }
	public IEnumerable<ITransaction> Inputs { get; }
	public IEnumerable<ITransaction> Outputs { get; }
	public DateTimeOffset Timestamp { get; }
	public int IncludedInBlock { get; }
	public Money OutputAmount { get; }
	public Money InputAmount { get; }
}

public class TransactionDetailsViewModelDesign : ITransactionDetailsViewModel
{
	public decimal FeeRate => (decimal) 11.2;
	public Money Fee => Money.Satoshis(1584);

	public IEnumerable<ITransaction> Inputs => new List<ITransaction>()
	{
		new TransactionDesign(Money.FromUnit((decimal) 0.00065536, MoneyUnit.BTC), "tb1qeteqj5u8j4ztx86dcmhhm9mjjv02y4yl0r3s9e" ),
	};

	public IEnumerable<ITransaction> Outputs => new List<ITransaction>()
	{
		new TransactionDesign(Money.FromUnit((decimal) 0.00050849, MoneyUnit.BTC), "miner8VH6WPrsQ1Fxqb7MPgJEoFYX2RCkS" ),
		new TransactionDesign(Money.FromUnit((decimal) 0.00013103, MoneyUnit.BTC), "tb1q2aq8nwmywk4qge39hcq0gd2tme0wqzx7pf7w93" ),
	};

	public DateTimeOffset Timestamp => new(2022, 8, 9, 12, 11, 0, 0, TimeSpan.FromHours(2));
	public int IncludedInBlock => 2315560;
	public Money OutputAmount => Money.Satoshis(1584);
	public Money InputAmount => Money.Satoshis(1584);
}

internal record TransactionDesign(Money Amount, string Address) : ITransaction;

public interface ITransaction
{
	public Money Amount { get; }
	public string Address { get; }
}
