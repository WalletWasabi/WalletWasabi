using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Client.Batching;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public delegate ImmutableList<SmartCoin> CoinSelector(SmartCoin[] coins, UtxoSelectionParameters parameters, Money liquidityClue);

public static class CoinSelectors
{
	public static CoinSelector ComposedCoinSelector(CoinSelector[] coinSelectors) =>
		(coins, parameters, liquidityClue) => coinSelectors
			.Select(coinSelector => coinSelector(coins, parameters, liquidityClue))
			.FirstOrDefault(selection => !selection.IsEmpty, ImmutableList<SmartCoin>.Empty);

	// Selects coins for as many pending payments as possible, cheapest first, and none if no payment can be made.
	// A payment signed in a transaction that was not broadcast can only be made in a transaction that also spends
	// an input of it, so an input of each such transaction that can still confirm is selected first. Red coins stay isolated.
	public static CoinSelector PrivatePaymentCoinSelector(PaymentBatch paymentBatch, int minAnonScoreForPayments, int semiPrivateThreshold) =>
		(coins, parameters, _) =>
		{
			var feeRate = parameters.MiningFeeRate;
			var candidates = coins
				.Where(parameters.Allows)
				.Where(x => x.IsPrivate(minAnonScoreForPayments))
				.ToArray();

			Money ValueOf(ImmutableList<SmartCoin> selection) =>
				selection.Sum(x => x.EffectiveValue(feeRate));

			SmartCoin[] Selectable(ImmutableList<SmartCoin> selection) =>
				candidates
					.Except(selection)
					.Where(x => !x.IsRedCoin(semiPrivateThreshold) || !selection.Any(y => y.IsRedCoin(semiPrivateThreshold)))
					.ToArray();

			ImmutableList<SmartCoin> SpendFrom(ImmutableList<SmartCoin> selection, ImmutableArray<OutPoint> attemptInputs)
			{
				var coin = Selectable(selection)
					.Where(x => attemptInputs.Contains(x.Outpoint))
					.OrderBy(x => x.IsRedCoin(semiPrivateThreshold))
					.ThenByDescending(x => x.EffectiveValue(feeRate))
					.FirstOrDefault();
				return selection.Any(x => attemptInputs.Contains(x.Outpoint)) || coin is null
					? selection
					: selection.Add(coin);
			}

			ImmutableList<SmartCoin> Fund(ImmutableList<SmartCoin> selection, Money amount)
			{
				var missing = amount - ValueOf(selection);
				var selectable = Selectable(selection);
				var coin = selectable.Where(x => x.EffectiveValue(feeRate) >= missing).MinBy(x => x.EffectiveValue(feeRate))
					?? selectable.MaxBy(x => x.EffectiveValue(feeRate));
				return missing <= Money.Zero || selection.Count >= CoinJoinCoinSelector.MaxInputsRegistrableByWallet || coin is null
					? selection
					: Fund(selection.Add(coin), amount);
			}

			var paymentsFunding = paymentBatch.GetPendingPayments()
				.OrderBy(payment => payment.EffectiveCost(feeRate))
				.Aggregate(
					(Coins: ImmutableList<SmartCoin>.Empty, Amount: Money.Zero),
					(funding, payment) =>
					{
						var amount = funding.Amount + payment.EffectiveCost(feeRate);
						var selection = Fund(paymentBatch.GetFailedAttemptInputSets(payment).Aggregate(funding.Coins, SpendFrom), amount);
						return selection.Count <= CoinJoinCoinSelector.MaxInputsRegistrableByWallet
							&& ValueOf(selection) >= amount
							&& paymentBatch.CanBeRetriedWith(payment, selection.Select(x => x.Outpoint).ToHashSet())
							? (selection, amount)
							: funding;
					});

			return paymentsFunding.Coins.ToShuffled(RandomnessProviders.Secure).ToImmutableList();
		};
}
