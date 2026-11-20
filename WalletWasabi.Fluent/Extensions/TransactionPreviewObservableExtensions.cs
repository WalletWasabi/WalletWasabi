using System;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.Extensions;

public static class TransactionPreviewObservableExtensions
{
	public static IObservable<BuildTransactionResult> ObserveInputChanges(
		this IObservable<BuildTransactionResult?> transactions) =>
		transactions
			.Select(transaction =>
				transaction is null
					? Observable.Empty<BuildTransactionResult>()
					: transaction.SpentCoins
						.Select(coin => coin.WhenAnyValue(
								x => x.Confirmed,
								x => x.CoinJoinInProgress,
								x => x.HdPubKey.AnonymitySet)
							.Skip(1)
							.Select(_ => transaction))
						.Merge()
						.StartWith(transaction))
			.Switch();
}
