using System.Collections.Generic;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class TransactionDetailsViewModelDesign : ITransactionViewModel
{
	public IObservable<int> Confirmations => Observable.Return(122);
	public FeeRate FeeRate => new((decimal) 2.45);
	public Money Fee => Money.Satoshis(1584);

	public IEnumerable<InputViewModel> Inputs => new List<InputViewModel>
	{
		new KnownInputViewModel(Money.FromUnit((decimal) 0.00065536, MoneyUnit.BTC), "tb1qeteqj5u8j4ztx86dcmhhm9mjjv02y4yl0r3s9e"),
		new UnknownInputViewModel(uint256.One),
	};

	public IEnumerable<OutputViewModel> Outputs => new List<OutputViewModel>
	{
		new(Money.FromUnit((decimal) 0.00050849, MoneyUnit.BTC), "miner8VH6WPrsQ1Fxqb7MPgJEoFYX2RCkS", true, new [] { Feature.RBF } ),
		new(Money.FromUnit((decimal) 0.00013103, MoneyUnit.BTC), "tb1q2aq8nwmywk4qge39hcq0gd2tme0wqzx7pf7w93", false, new [] { Feature.SegWit })
	};

	public DateTimeOffset Timestamp => new(2022, 8, 9, 12, 11, 0, 0, TimeSpan.FromHours(2));
	public int IncludedInBlock => 2315560;
	public Money OutputAmount => Money.Satoshis(1584);
	public Money InputAmount => Money.Satoshis(1584);
	public Money Amount => Money.FromUnit(12020, MoneyUnit.Satoshi);
	public string Id => "a3e405466e2352a5ef21045f8e214f381282a9e498f7709be9d3ce8c8618e12d";
	public double Size => 46.82d;
	public int Version => 1;
	public long BlockTime => 0;
	public double Weight => 106.07;
	public double VirtualSize => 26.52;
	public IEnumerable<string> Labels => new[] { "Test1", "Test2", "Test3" };
	public IEnumerable<Feature> Features => this.Features();
}
