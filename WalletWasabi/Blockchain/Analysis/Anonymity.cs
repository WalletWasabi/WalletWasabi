using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using AnonymityScore = decimal;

namespace WalletWasabi.Blockchain.Analysis;

public static class SmartTransactionExtensions
{
	public static bool IsSelfSpend(this SmartTransaction tx) => tx.ForeignInputs.Count == 0 && tx.ForeignOutputs.Count == 0;
	public static bool IsMultiparty(this SmartTransaction tx) => tx.ForeignInputs.Count > 0 && tx.WalletInputs.Count > 0;
}


// This immutable record manages the state of anonymity scores.
// The dependency tracking system is because a coin's privacy is affected by its transaction history.
// When the privacy of one coin changes, all dependent coins need recalculation.
public record AnonymityScoreDb
{
	// Tracks which coins have been analyzed
	private ImmutableHashSet<SmartCoin> SmartCoinScores { get; init; } =
		ImmutableHashSet<SmartCoin>.Empty;

	// Stores the actual anonymity score for each public key
	private ImmutableDictionary<HdPubKey, AnonymityScore> PubKeyScores { get; init; } =
		ImmutableDictionary<HdPubKey, AnonymityScore>.Empty;

	// Records which coins depend on others for their privacy calculation
	private ImmutableList<(HdPubKey Source, SmartCoin SmartCoin)> Dependencies { get; init; } =
		ImmutableList<(HdPubKey Source, SmartCoin SmartCoin)>.Empty;

	public static readonly AnonymityScoreDb Empty = new();

	// Tries to retrieve a cached anonymity score for a coin. If the coin exists in the cache,
	// it returns the score associated with the coin's public key. This design reflects the fact
	// that all coins associated with the same public key share the same privacy level.
	public bool TryGetAnonymityScore(SmartCoin coin, [NotNullWhen(true)] out AnonymityScore? anonymityScore)
	{
		if (SmartCoinScores.Contains(coin))
		{
			anonymityScore = PubKeyScores[coin.HdPubKey];
			return true;
		}

		anonymityScore = null;
		return false;
	}

	// Tries to retrieve a cached anonymity score for a public key.
	public bool TryGetAnonymityScore(HdPubKey pubkey, [NotNullWhen(true)] out AnonymityScore? anonymityScore)
	{
		if (PubKeyScores.TryGetValue(pubkey, out var storedAnonymityScore))
		{
			anonymityScore = storedAnonymityScore;
			return true;
		}

		anonymityScore = null;
		return false;
	}

	// Sets an anonymity score for a coin.
	public AnonymityScoreDb SetAnonymityScore (SmartCoin coin, AnonymityScore anonymityScore)
	{
		var invalidated = InvalidateCacheEntries(coin.HdPubKey);
		return invalidated with
		{
			SmartCoinScores = invalidated.SmartCoinScores.Add(coin),
			PubKeyScores = invalidated.PubKeyScores.SetItem(coin.HdPubKey, anonymityScore),
			Dependencies =
				invalidated.Dependencies.AddRange(coin.Transaction.WalletInputs.Select(scoin => (scoin.HdPubKey, coin)))
		};
	}

	// Invalidates all cache entries that depend on a particular public key. This ensures
	// that when a coin's privacy changes, all dependent coins get recalculated properly.
	private AnonymityScoreDb InvalidateCacheEntries(HdPubKey pubKey)
	{
		var dependencies = Dependencies.Where(x => x.Source == pubKey).ToArray();
		var validDependencies = dependencies.Aggregate(this, (db, dependency) =>
			db.InvalidateCacheEntries(dependency.SmartCoin.HdPubKey));

		return validDependencies with
		{
			Dependencies = Dependencies.RemoveRange(dependencies),
			SmartCoinScores = SmartCoinScores.Except(dependencies.Select(x => x.SmartCoin)),
			PubKeyScores = PubKeyScores.RemoveRange(dependencies.Select(x => x.SmartCoin.HdPubKey))
		};
	}
}

public static class Anonymity
{
	public static (AnonymityScore AnonymityScore, AnonymityScoreDb Db) GetScore(SmartCoin coin, AnonymityScoreDb db)
	{
		if (db.TryGetAnonymityScore(coin, out var anonymityScore))
		{
			return ((AnonymityScore)anonymityScore, db);
		}

		var (calculatedScore, pdb) = AnalyzeCoinAnonymity(coin, db); // Calculate the anonscore
		if (db.TryGetAnonymityScore(coin.HdPubKey, out var storedAnonymityScore))
		{
			if (calculatedScore > storedAnonymityScore)
			{
				pdb = pdb.SetAnonymityScore(coin, calculatedScore); // Stores it if is less than value already known
				return (calculatedScore, pdb);
			}

			return ((AnonymityScore)storedAnonymityScore, pdb);
		}

		pdb = pdb.SetAnonymityScore(coin, calculatedScore); // Stores it if is less than value already known
		return (calculatedScore, pdb);
	}


	private static (AnonymityScore, AnonymityScoreDb) AnalyzeCoinAnonymity(SmartCoin coin, AnonymityScoreDb db)
	{
		var tx = coin.Transaction;
		if (tx.IsSelfSpend())
		{
			// You can't gain privacy by sending to yourself but you can be penalized by consolidating.
			return CalculateMinimumScoreFromInputs();
		}

		if (tx.IsMultiparty())
		{
			var (minimumAnonScore, updatedDb) = CalculateMinimumScoreFromInputs();
			var anonymityGain = Math.Max(ComputeAnonymityContribution(coin), 1m / tx.ForeignInputs.Count );
			anonymityGain = anonymityGain > 1 ? 1 / anonymityGain : anonymityGain;

			return (minimumAnonScore * anonymityGain, updatedDb);
		}

		// When you send and receive change or, when you receive a payment, the coin is known.
		return (1m, db);

		(AnonymityScore, AnonymityScoreDb) CalculateMinimumScoreFromInputs()
		{
			var (updatedDb, anonymityScoreSum) = tx.WalletVirtualInputs.Aggregate(
				(scoreDb: db, anonymityScore: 0m), (acc, virtualInput) =>
				{
					var (anonymityScore, scoreDb) = GetScore(virtualInput.Coins.First(), acc.scoreDb);
					return (scoreDb, acc.anonymityScore + anonymityScore);
				});
			// Knowledge about coins is not mutually exclusive, and then addition can result in numbers bigger than one.
			return (Math.Min(1m, anonymityScoreSum), updatedDb);
		}
	}


	private static AnonymityScore ComputeAnonymityContribution(SmartCoin transactionOutput)
	{
		var walletVirtualOutputs = transactionOutput.Transaction.WalletVirtualOutputs;
		var foreignVirtualOutputs = transactionOutput.Transaction.ForeignVirtualOutputs;

		var amount = walletVirtualOutputs.First(o => o.Coins.Select(c => c.Outpoint).Contains(transactionOutput.Outpoint)).Amount;

		// Count the outputs that have the same value as our transactionOutput.
		var equalValueWalletVirtualOutputCount = walletVirtualOutputs.Count(o => o.Amount == amount);
		var equalValueForeignRelevantVirtualOutputCount = foreignVirtualOutputs.Count(o => o.Amount == amount);

		return equalValueForeignRelevantVirtualOutputCount > 0
			? (AnonymityScore)equalValueWalletVirtualOutputCount / equalValueForeignRelevantVirtualOutputCount
			: 1m;
	}
}
