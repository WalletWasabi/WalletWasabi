using WalletWasabi.WabiSabi.Client.Batching;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public delegate ImmutableList<SmartCoin> CoinSelector(SmartCoin[] coins, UtxoSelectionParameters parameters, Money liquidityClue);

public static class CoinSelectors
{
	// Arbitrary number.
	private const int RandomSelectionAttempts = 100;

	public static CoinSelector ComposedCoinSelector(CoinSelector[] coinSelectors) =>
		(coins, parameters, liquidityClue) => coinSelectors
			.Select(coinSelector => coinSelector(coins, parameters, liquidityClue))
			.FirstOrDefault(selection => !selection.IsEmpty, ImmutableList<SmartCoin>.Empty);

	// Pays as many pending payments as possible, cheapest first, with as many random coins as a coinjoin would mix.
	// The selection spends an input of each failed attempt, leaves more than the smallest denomination behind so
	// that no dust is left, while respecting red coins isolation.
	public static CoinSelector PrivatePaymentCoinSelector(PaymentBatch paymentBatch, int minAnonScoreForPayments, int semiPrivateThreshold, CoinJoinCoinSelectorRandomnessGenerator generator) =>
		(coins, parameters, _) =>
		{
			var feeRate = parameters.MiningFeeRate;
			var candidates = coins
				.Where(parameters.Allows)
				.Where(x => x.IsPrivate(minAnonScoreForPayments))
				.ToArray();

			Money ValueOf(ImmutableList<SmartCoin> selection) =>
				selection.Sum(x => x.EffectiveValue(feeRate));

			bool Funds(ImmutableList<SmartCoin> selection, Money amount) =>
				ValueOf(selection) > amount + parameters.MinAllowedOutputAmount;

			SmartCoin[] Selectable(ImmutableList<SmartCoin> selection) =>
				candidates
					.Except(selection)
					.Where(x => !x.IsRedCoin(semiPrivateThreshold) || !selection.Any(y => y.IsRedCoin(semiPrivateThreshold)))
					.ToArray();

			ImmutableList<SmartCoin> SpendFrom(ImmutableList<SmartCoin> selection, ImmutableArray<OutPoint> attemptInputs) =>
				selection.Any(x => attemptInputs.Contains(x.Outpoint))
					? selection
					: selection.AddRange(Selectable(selection)
						.Where(x => attemptInputs.Contains(x.Outpoint))
						.OrderBy(x => x.IsRedCoin(semiPrivateThreshold))
						.ThenByDescending(x => x.EffectiveValue(feeRate))
						.Take(1));

			ImmutableList<SmartCoin> Draw(ImmutableList<SmartCoin> selection, IList<SmartCoin> pool, int inputCount) =>
				pool.Aggregate(selection, (drawn, coin) => drawn.Count < inputCount && Selectable(drawn).Contains(coin) ? drawn.Add(coin) : drawn);

			ImmutableList<SmartCoin> Fund(ImmutableList<SmartCoin> selection, Money amount) =>
				Enumerable.Range(0, RandomSelectionAttempts)
					.Select(_ => Draw(selection, Selectable(selection).ToShuffled(generator.Rnd), generator.GetInputTarget()))
					.FirstOrDefault(drawn => Funds(drawn, amount))
					?? Draw(selection, Selectable(selection).OrderByDescending(x => x.EffectiveValue(feeRate)).ToArray(), CoinJoinCoinSelector.MaxInputsRegistrableByWallet);

			var paymentsFunding = paymentBatch.GetPendingPayments()
				.OrderBy(payment => payment.EffectiveCost(feeRate))
				.Aggregate(
					(Coins: ImmutableList<SmartCoin>.Empty, Payments: ImmutableList<Payment>.Empty),
					(funding, payment) =>
					{
						var payments = funding.Payments.Add(payment);
						var amount = new PaymentSet(payments, feeRate).TotalAmount;
						var selection = Fund(paymentBatch.GetFailedAttemptInputSets(payment).Aggregate(funding.Coins, SpendFrom), amount);
						return selection.Count <= CoinJoinCoinSelector.MaxInputsRegistrableByWallet
							&& Funds(selection, amount)
							&& paymentBatch.CanBeRetriedWith(payment, selection.Select(x => x.Outpoint).ToHashSet())
							? (selection, payments)
							: funding;
					});

			var selection = paymentsFunding.Coins.ToShuffled(generator.Rnd).ToImmutableList();
			if (!selection.IsEmpty)
			{
				Logger.LogDebug($"Selected {selection.Count} coins to pay {string.Join(", ", paymentsFunding.Payments.Select(x => x.Amount.ToString(false, true)))} BTC: {string.Join(", ", selection.Select(x => x.Amount.ToString(false, true)))} BTC.");
			}

			return selection;
		};
}
