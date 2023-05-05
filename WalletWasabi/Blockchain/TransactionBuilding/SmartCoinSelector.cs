using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Exceptions;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using System.Collections.Immutable;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class SmartCoinSelector : ICoinSelector
{
	public SmartCoinSelector(List<SmartCoin> unspentCoins, SmartLabel recipient, int privateThreshold, int semiPrivateThreshold)
	{
		UnspentCoins = unspentCoins.Distinct().ToList();
		Recipient = recipient;
		PrivateThreshold = privateThreshold;
		SemiPrivateThreshold = semiPrivateThreshold;
	}

	private List<SmartCoin> UnspentCoins { get; }
	public SmartLabel Recipient { get; }
	public int PrivateThreshold { get; }
	public int SemiPrivateThreshold { get; }
	private int IterationCount { get; set; }
	private Exception? LastTransactionSizeException { get; set; }

	/// <param name="suggestion">We use this to detect if NBitcoin tries to suggest something different and indicate the error.</param>
	/// <param name="target">Only <see cref="Money"/> type is really supported by this implementation.</param>
	/// <remarks>Do not call this method repeatedly on a single <see cref="SmartCoinSelector"/> instance.</remarks>
	public IEnumerable<ICoin> Select(IEnumerable<ICoin> suggestion, IMoney target)
	{
		var targetMoney = (Money)target;

		long available = UnspentCoins.Sum(x => x.Amount);
		if (available < targetMoney)
		{
			throw new InsufficientBalanceException(targetMoney, available);
		}

		if (IterationCount > 500)
		{
			if (LastTransactionSizeException is not null)
			{
				throw LastTransactionSizeException;
			}

			throw new TimeoutException("Coin selection timed out.");
		}

		// The first iteration should never take suggested coins into account .
		if (IterationCount > 0)
		{
			Money suggestedSum = Money.Satoshis(suggestion.Sum(c => (Money)c.Amount));
			if (suggestedSum < targetMoney)
			{
				LastTransactionSizeException = new TransactionSizeException(targetMoney, suggestedSum);
			}
		}

		var coins = FilterUnnecessaryPrivateAndSemiPrivateUnconfirmedCoins(UnspentCoins, targetMoney);
		var pockets = coins.ToPockets(PrivateThreshold).ToArray();
		var bestPocket = GetBestCombination(pockets, targetMoney);
		var bestPocketCoins = bestPocket.Coins;

		var coinsInBestPocketByScript = bestPocketCoins
			.GroupBy(c => c.ScriptPubKey)
			.Select(group => (ScriptPubKey: group.Key, Coins: group.ToList()))
			.OrderByDescending(x => x.Coins.Sum(c => c.Amount))
			.ToImmutableList();

		// {1} {2} ... {n} {1, 2} {1, 2, 3} {1, 2, 3, 4} ... {1, 2, 3, 4, 5 ... n}
		var coinsGroup = coinsInBestPocketByScript.Select(x => ImmutableList.Create(x))
			.Concat(coinsInBestPocketByScript.Scan(ImmutableList<(Script ScriptPubKey, List<SmartCoin> Coins)>.Empty, (acc, coinGroup) => acc.Add(coinGroup)));

		// Flattens the groups of coins and filters out the ones that are too small.
		// Finally it sorts the solutions by amount and coins (those with less coins on the top).
		var candidates = coinsGroup
			.Select(x => x.SelectMany(y => y.Coins))
			.Select(x => (Coins: x, Total: x.Sum(y => y.Amount), AnonScoreAverage: x.Sum(y => y.AnonymitySet) / x.Count()))
			.Where(x => x.Total >= targetMoney) // filter combinations below target
			.OrderBy(x => x.Total) // the closer we are to the target the better
			// .ThenByDescending(x => x.AnonScoreAverage) // Higher number means better privacy
			.ThenBy(x => x.Coins.Count());      // prefer lesser coin selection on the same amount

		IterationCount++;

		// Select the best solution.
		return candidates.First().Coins.Select(x => x.Coin);
	}

	/// <summary>
	/// Removes the unconfirmed Private and Semi-Private coins if they are not required.
	/// Since those two pockets are a mix of coins from different clusters,
	/// it is not mandatory to spend them as a whole pocket, so unconfirmed coins can be skipped.
	/// </summary>
	private IEnumerable<SmartCoin> FilterUnnecessaryPrivateAndSemiPrivateUnconfirmedCoins(IEnumerable<SmartCoin> unspentCoins, Money targetAmount)
	{
		SmartCoin[] FilterIfUnnecessary(SmartCoin[] allCoins, SmartCoin[] coinsToFilter)
		{
			return allCoins.Sum(x => x.Amount) - coinsToFilter.Sum(x => x.Amount) >= targetAmount
				? allCoins.Except(coinsToFilter).ToArray()
				: allCoins;
		}

		var unconfirmedSemiPrivateCoins = UnspentCoins.Where(x => x.IsSemiPrivate(PrivateThreshold, SemiPrivateThreshold) && !x.Confirmed).ToArray();
		var unconfirmedPrivateCoins = UnspentCoins.Where(x => x.IsPrivate(PrivateThreshold) && !x.Confirmed).ToArray();
		var coins = unspentCoins.ToArray();

		coins = FilterIfUnnecessary(coins, unconfirmedSemiPrivateCoins);
		coins = FilterIfUnnecessary(coins, unconfirmedPrivateCoins);

		return coins;
	}

	/// <summary>
	/// Calculates the best combination from the gives pocket that can cover the target amount,
	/// if the calculation is not expensive.
	/// Otherwise it returns a combination fromm all pockets.
	/// </summary>
	private Pocket GetBestCombination(Pocket[] pockets, Money targetMoney)
	{
		var pocketsWithEnoughAmount = pockets.Where(x => x.Amount >= targetMoney).ToArray();
		var pocketWithNotEnoughAmount = pockets.Except(pocketsWithEnoughAmount).ToArray();

		if (pocketWithNotEnoughAmount.Length >= 10)
		{
			return Pocket.Merge(pockets);
		}

		return pocketWithNotEnoughAmount
			.CombinationsWithoutRepetition(ofLength: 2, upToLength: 6)
			.Union(pocketsWithEnoughAmount.Select(x => new[] { x }))
			.Select(pocketCombination =>
				(Score: pocketCombination.Max(GetPrivacyScore),
					Pocket: Pocket.Merge(pocketCombination.ToArray()),
					Unconfirmed: pocketCombination.Any(x => x.IsUnconfirmed())))
			.Where(x => x.Pocket.Amount >= targetMoney)
			.OrderBy(x => x.Unconfirmed)
			.ThenBy(x => x.Score)
			.ThenByDescending(x => x.Pocket.Coins.Sum(x => x.HdPubKey.AnonymitySet) / x.Pocket.Coins.Count())
			.ThenBy(x => x.Pocket.Amount)
			.First()
			.Pocket;
	}

	/// <summary>
	/// Scores the given pocket from a privacy acceptance perspective.
	/// </summary>
	private decimal GetPrivacyScore(Pocket pocket)
	{
		if (Recipient.Equals(pocket.Labels, StringComparer.OrdinalIgnoreCase))
		{
			return 1;
		}

		if (pocket.IsPrivate(PrivateThreshold))
		{
			return 2;
		}

		if (pocket.IsSemiPrivate(PrivateThreshold, SemiPrivateThreshold))
		{
			return 3;
		}

		if (pocket.IsUnknown(SemiPrivateThreshold))
		{
			return 8;
		}

		var containedRecipientLabelsCount = pocket.Labels.Count(label => Recipient.Contains(label, StringComparer.OrdinalIgnoreCase));
		if (containedRecipientLabelsCount > 0)
		{
			var index = ((decimal)containedRecipientLabelsCount / pocket.Labels.Count) + ((decimal)containedRecipientLabelsCount / Recipient.Count);
			return 4 + (2 - index);
		}

		return 7 + (1 - 1M / pocket.Labels.Count);
	}
}
