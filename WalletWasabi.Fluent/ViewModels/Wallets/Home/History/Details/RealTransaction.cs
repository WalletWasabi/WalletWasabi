using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class RealTransaction
{
	public RealTransaction(RealWallete realWallete, uint256 id)
	{
		var obs = realWallete.SomethingChanged
			.Select(_ => GetTransaction(realWallete, id))
			.StartWith(GetTransaction(realWallete, id))
			.WhereNotNull();

		Amount = obs.Select(x => x.Amount);
	}

	public IObservable<Money> Amount { get; }

	private static TransactionSummary? GetTransaction(RealWallete wallet, uint256 id)
	{
		var summary = wallet.HistoryBuilder.BuildHistorySummary();
		return summary.FirstOrDefault(tx => tx.TransactionId == id);
	}
}
