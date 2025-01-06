using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.Wallets;
using SecureRandom = WabiSabi.Crypto.Randomness.SecureRandom;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class CoinJoinCoinSelector
{
	public const int MaxInputsRegistrableByWallet = 15; // how many

	/// <param name="anonScoreTarget">Tries to select few coins over this threshold.</param>
	/// <param name="semiPrivateThreshold">Minimum anonymity of coins that can be selected together.</param>
	public CoinJoinCoinSelector(
		int anonScoreTarget,
		int semiPrivateThreshold,
		bool redCoinIsolation,
		OutputProvider outputProvider,
		WasabiRandom? random = null)
	{
		AnonScoreTarget = anonScoreTarget;
		SemiPrivateThreshold = semiPrivateThreshold;
		RedCoinIsolation = redCoinIsolation;
		OutputProvider = outputProvider;

		_rnd = random ?? SecureRandom.Instance;
	}

	public int AnonScoreTarget { get; }
	public int SemiPrivateThreshold { get; }
	public bool RedCoinIsolation { get; }
	public OutputProvider OutputProvider { get; }

	private readonly WasabiRandom _rnd;

	public static CoinJoinCoinSelector FromWallet(IWallet wallet) =>
		new(
			wallet.AnonScoreTarget,
			Constants.SemiPrivateThreshold,
			wallet.RedCoinIsolation,
			wallet.OutputProvider);

	public ImmutableList<TCoin> SelectCoinsForRound<TCoin>(IEnumerable<TCoin> coins, UtxoSelectionParameters parameters)
		where TCoin : class, ISmartCoin, IEquatable<TCoin>
	{
		var filteredCoins = coins
			.Where(x => parameters.AllowedInputAmounts.Contains(x.Amount))
			.Where(x => parameters.AllowedInputScriptTypes.Contains(x.ScriptType))
			.Where(x => x.EffectiveValue(parameters.MiningFeeRate) > Money.Zero)
			.ToList();

		// Sanity check.
		if (filteredCoins.Count == 0)
		{
			Logger.LogDebug("No suitable coins for this round.");
			return ImmutableList<TCoin>.Empty;
		}

		var privateCoins = filteredCoins
			.Where(x => x.IsPrivate(AnonScoreTarget))
			.ToArray();
		var semiPrivateCoins = filteredCoins
			.Where(x => x.IsSemiPrivate(AnonScoreTarget, SemiPrivateThreshold))
			.ToArray();
		var redCoins = filteredCoins
			.Where(x => x.IsRedCoin(SemiPrivateThreshold))
			.ToArray();

		if (semiPrivateCoins.Length + redCoins.Length == 0)
		{
			Logger.LogDebug("No suitable coins for this round.");
			return ImmutableList<TCoin>.Empty;
		}

		Logger.LogDebug($"Coin selection started:");
		Logger.LogDebug(
			$"{nameof(filteredCoins)}: {filteredCoins.Count} coins, valued at {Money.Satoshis(filteredCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug(
			$"{nameof(privateCoins)}: {privateCoins.Length} coins, valued at {Money.Satoshis(privateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug(
			$"{nameof(semiPrivateCoins)}: {semiPrivateCoins.Length} coins, valued at {Money.Satoshis(semiPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug(
			$"{nameof(redCoins)}: {redCoins.Length} coins, valued at {Money.Satoshis(redCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		List<TCoin> winner = [];

		// PAYMENTS IF ENOUGH PRIVATE COINS
		// If there are enough private coins, we can perform payments. It is more important than increasing privacy.
		// An interesting improvements could be to participate in parallel to the same round.
		PaymentAwareOutputProvider? paymentAwareOutputProvider = null;
		if(OutputProvider is PaymentAwareOutputProvider provider)
		{
			paymentAwareOutputProvider = provider;

			var payments = paymentAwareOutputProvider.BatchedPayments
				.GetPayments()
				.Where(x => x.State is PendingPayment)
				.ToList();

			var privateCoinsSortedByEffectiveValue = privateCoins
				.OrderByDescending(x => x.EffectiveValue(parameters.MiningFeeRate))
				.ToArray();

			var availablePrivateEffectiveValue = privateCoinsSortedByEffectiveValue
				.Select(x => x.EffectiveValue(parameters.MiningFeeRate))
				.Take(MaxInputsRegistrableByWallet)
				.Sum(x => x);

			// TODO: We need to account for the Shared Overhead here
			// TODO: A solution is to add 2 new parameters to the UtxoSelectionParameters:
			// TODO: - Total Overhead to pay
			// TODO: - Min number of inputs possible
			// TODO: So this client could account the max overhead he will have to pay.
			availablePrivateEffectiveValue -= 0;

			// Don't touch the priority provided by PaymentAwareOutputProvider.
			// This only works because there is no
			var toPerformPayments = new List<Payment>();
			foreach (var payment in payments)
			{
				if (availablePrivateEffectiveValue < payment.Amount)
				{
					continue;
				}

				toPerformPayments.Add(payment);
				availablePrivateEffectiveValue -= payment.Amount;
			}

			if (toPerformPayments.Count != 0)
			{
				// TODO: Again the Shared Overhead should be accounted here.
				var totalToPerformPaymentsAmount = toPerformPayments.Sum(x => x.Amount) + 0;

				// TODO: Here we can improve by searching the combination of coins that minimize both the number of coins used and the change .
				// TODO: I'm only making a small optimization here, that might be enough:
				// TODO: If adding a coin would be enough to make the payments, I search to see if there are lower coins that would also be enough.
				foreach (var coin in privateCoinsSortedByEffectiveValue)
				{
					var currentTotalEffectiveValue = winner.Sum(x => x.EffectiveValue(parameters.MiningFeeRate));

					if (currentTotalEffectiveValue + coin.EffectiveValue(parameters.MiningFeeRate) >= totalToPerformPaymentsAmount)
					{
						// Adding the coin would be enough, so search for lower coins that would also be enough.
						// We are certain to find one here because of previous condition. Worst case scenario: lowerCoin = coin.
						foreach (var lowerCoin in privateCoinsSortedByEffectiveValue.Reverse())
						{
							if(currentTotalEffectiveValue + coin.EffectiveValue(parameters.MiningFeeRate) >= totalToPerformPaymentsAmount)
							{
								winner.Add(lowerCoin);
							}
						}

						break;
					}

					winner.Add(coin);
				}

				// Because the AmountDecomposer is currently not made to accept payments + consolidating or gaining privacy at the same time,
				// it is better to return here and not add any other coin. So this round will only be used to perform the payments.
				// This can cause issues if an user is consistently trying to perform small payments: he will never gain privacy as he will always be performing payments.
				return winner.ToShuffled(_rnd).ToImmutableList();
			}
		}

		// COINS SHARING SCRIPT PUB KEY
		// Those have the highest of all priorities. We select as many as we can.
		// Only MaxInputsRegistrableByWallet can limit amount selected or RedCoinIsolation if several non-unique ScriptPubKeys are available.
		var samePubKeyGroups = filteredCoins
			.GroupBy(x => x.ScriptPubKey)
			.Where(x => x.Count() > 1)
			.OrderByDescending(x => x.Count())
			.ToList();

		foreach (var group in samePubKeyGroups)
		{
			foreach (var coin in group.OrderByDescending(x => x.Amount))
			{
				if (winner.Count >= MaxInputsRegistrableByWallet)
				{
					return winner.ToShuffled(_rnd).ToImmutableList();
				}

				filteredCoins.Remove(coin);
				winner.Add(coin);
			}

			// RedCoinIsolation is used differently here:
			// It doesn't isolate coins from the same ScriptPubKey, but it isolates them from other coins.
			// Note: There shouldn't be non-red coins on the same ScriptPubKey. If they are, they will be isolated
			if (RedCoinIsolation)
			{
				break;
			}
		}

		// RED COINS
		// We want to select bigger red coins earlier to increase privacy score faster.
		redCoins = redCoins.OrderByDescending(x => x.Amount).ToArray();

		if (RedCoinIsolation)
		{
			// This condition can be false if some red-coins were added because they are sharing the same ScriptPubKey.
			if (winner.All(x => !x.IsRedCoin()))
			{
				// Bias is added to break determinism.
				var winnerRedCoin = redCoins.BiasedRandomElement(75, _rnd)!;
				winner.Add(winnerRedCoin);
			}
		}
		else
		{
			// If red-coins are not isolated, we can add them to the semi-private coins.
			semiPrivateCoins = semiPrivateCoins.Union(redCoins).ToArray();
		}

		// SEMI-PRIVATE COINS
		// We want to select lower anonscore coins earlier to avoid disasters while spending.
		// We then want to select bigger coins earlier to increase privacy score faster.
		// But we want to avoid as much as possible selecting coins coming from the same Tx.

		// First order by AnonScore (lowest = highest priority) and Amount (highest = lowest priority).
		var partiallyOrderedSemiPrivateCoins = semiPrivateCoins
			.OrderBy(x => x.AnonymitySet)
			.ThenByDescending(x => x.Amount)
			.ToList();

		// Then we want to avoid selecting coins coming from the same Tx, so we deprioritize them.
		// This query is complex because it needs to interleave groups of TxId while keeping original order.
		// Ex: [1a, 1b, 2a, 2b, 1c, 3a] -> [1a, 2a, 3a, 1b, 2b, 1c] (with digit = TxId)
		var fullyOrderedSemiPrivateCoins = partiallyOrderedSemiPrivateCoins
			.Select((coin, index) => new { Coin = coin, OriginalIndex = index })
			.GroupBy(x => x.Coin.TransactionId)
			.Select(g => new { Group = g, FirstIndex = g.Min(x => x.OriginalIndex) })
			.OrderBy(g => g.FirstIndex)
			.GroupBy(x => true) // We need to group them to be able to use the count in SelectMany
			.SelectMany(g => g.SelectMany(groupInfo =>
				groupInfo.Group.Select((x, indexInGroup) => new
				{
					x.Coin,
					Position = indexInGroup * g.Count() + groupInfo.FirstIndex
				})))
			.OrderBy(x => x.Position)
			.Select(x => x.Coin)
			.ToList();

		var currentIndex = 0;
		foreach (var coin in fullyOrderedSemiPrivateCoins)
		{
			// We want to break determinism as usual. This bias cannot be too big because coins are ordered by priority
			// and in some cases not including the first coins might decrease significantly performance.
			const double BasePercentageOfSemiPrivateCoinsInclusion = 95.0;

			var availableSpots = MaxInputsRegistrableByWallet - winner.Count;

			if(availableSpots <= 0)
			{
				return winner.ToShuffled(_rnd).ToImmutableList();
			}

			// We enumerate the winner instead of keeping track because it's cheap anyway some red coins from same TxId might already be in there.
			var numberOfCoinsAddedFromThisTx = winner.Count(x => x.TransactionId == coin.TransactionId);

			// Base inclusion has to be very high at the begining to not skip the most important coins
			// however we can lower it quickly to avoid any potential deterministic behavior.
			// We lower so if index is MaxInputsRegistrableByWallet then penalty will be 25%.
			var baseInclusionPenalty = currentIndex * (BasePercentageOfSemiPrivateCoinsInclusion / (MaxInputsRegistrableByWallet * 4.0));

			// We want to avoid selecting coins coming from the same Tx, so there must be a logarithmic penalty.
			// But we have a lot of spots, we should be less picky with this restriction and so we lower the penalty with a boost.
			var spotsRatio = (double)availableSpots / MaxInputsRegistrableByWallet;
			var boost = 1 + spotsRatio;  // This will range from 1 to 2
			var sameTxPenaltyRatio = 1 / Math.Pow(numberOfCoinsAddedFromThisTx / boost, 2);

			var finalPercentageOfInclusion = (BasePercentageOfSemiPrivateCoinsInclusion - baseInclusionPenalty) * sameTxPenaltyRatio;

			if(_rnd.GetInt(0, 101) < finalPercentageOfInclusion)
			{
				winner.Add(coin);
			}

			currentIndex++;
		}

		// PRIVATE COINS
		// Choosing which private coins to add is by far the most difficult.
		// There is no actual need to select these coins.
		// In the standard case, selecting private coins must be only for straight improvements.
		// We want to select private coins to:
		// - Consolidate them to perform payments or (currently not implemented) if fees are low.
		// - Keep a nice distribution of coins to reduce change for payments outside of rounds
		// - Reduce "toxic-change" risks during decomposition
		// - Reduce determinism regarding the Anon Score Target
		// We also need to keep risk of Anon Score Loss low, otherwise selecting those coins can be very expansive.

		//
		if(paymentAwareOutputProvider is not null)
		{
			var payments = paymentAwareOutputProvider.BatchedPayments;
		}
		return winner.ToShuffled(_rnd).ToImmutableList();
}
