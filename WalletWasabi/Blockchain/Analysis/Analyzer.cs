using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.Analysis;

public static class SmartTransactionExtensions
{
	public static bool IsReceive(this SmartTransaction tx) => tx.ForeignInputs.Count > 0 || tx.ForeignOutputs.Count > 0;
	public static bool IsSpend(this SmartTransaction tx) => tx.ForeignInputs.Count == 0 && tx.ForeignOutputs.Count > 0;
	public static bool IsSelfSpend(this SmartTransaction tx) => tx.ForeignInputs.Count == 0 && tx.ForeignOutputs.Count == 0;
	public static bool IsMultiparty(this SmartTransaction tx) => tx.ForeignInputs.Count > 0 && tx.WalletInputs.Count > 0;
}

public static class Analyzer
{
	public static readonly long[] StdDenoms = new[]
	{
		5000L, 6561L, 8192L, 10000L, 13122L, 16384L, 19683L, 20000L, 32768L, 39366L, 50000L, 59049L, 65536L, 100000L, 118098L,
		131072L, 177147L, 200000L, 262144L, 354294L, 500000L, 524288L, 531441L, 1000000L, 1048576L, 1062882L, 1594323L, 2000000L,
		2097152L, 3188646L, 4194304L, 4782969L, 5000000L, 8388608L, 9565938L, 10000000L, 14348907L, 16777216L, 20000000L,
		28697814L, 33554432L, 43046721L, 50000000L, 67108864L, 86093442L, 100000000L, 129140163L, 134217728L, 200000000L,
		258280326L, 268435456L, 387420489L, 500000000L, 536870912L, 774840978L, 1000000000L, 1073741824L, 1162261467L,
		2000000000L, 2147483648L, 2324522934L, 3486784401L, 4294967296L, 5000000000L, 6973568802L, 8589934592L, 10000000000L,
		10460353203L, 17179869184L, 20000000000L, 20920706406L, 31381059609L, 34359738368L, 50000000000L, 62762119218L,
		68719476736L, 94143178827L, 100000000000L, 137438953472L
	};

	public static double GetAnonymityScore(SmartCoin coin)
	{
		var anonScore = InternalAnalyzeCoin(coin);
		return anonScore;
	}

	private static double InternalAnalyzeCoin(SmartCoin coin)
	{
		var tx = coin.Transaction;
		if (tx.IsSelfSpend())
		{
			return Intersect(tx.WalletInputs.Select(GetAnonymityScore));
		}

		if (tx.IsMultiparty())
		{
			var minimumAnonScore = Intersect(tx.WalletInputs.Select(GetAnonymityScore));
			var anonymityGain = Math.Min(ComputeAnonymityContribution(coin), tx.ForeignInputs.Count);

			return minimumAnonScore + anonymityGain;
		}

		return 1d;
	}

	private static double Intersect(IEnumerable<double> anonsets)
	{
		// Our smallest anonset is relevant here, because anonsets cannot grow by intersection punishments.
		var smallestAnon = anonsets.Min();

		// Punish intersection exponentially.
		// If there is only a single anonset then the exponent should be zero to divide by 1 thus retain the input coin anonset.
		var intersectPenalty = Math.Pow(2, anonsets.Count() - 1);
		var intersectionAnonset = smallestAnon / Math.Max(1, intersectPenalty);

		// The minimum anonymity set size is 1, enforce it when the punishment is very large.
		return Math.Max(1d, intersectionAnonset);
	}

	/// <summary>
	/// Computes how much the foreign outputs of AnalyzedTransaction contribute to the anonymity of our transactionOutput.
	/// Sometimes we are only interested in how much a certain subset of foreign outputs contributed.
	/// This subset can be specified in relevantOutpoints, otherwise all outputs are considered relevant.
	/// </summary>
	private static double ComputeAnonymityContribution(SmartCoin transactionOutput)
	{
		var walletVirtualOutputs = transactionOutput.Transaction.WalletVirtualOutputs;
		var foreignVirtualOutputs = transactionOutput.Transaction.ForeignVirtualOutputs;

		var amount = walletVirtualOutputs.First(o => o.Coins.Select(c => c.Outpoint).Contains(transactionOutput.Outpoint)).Amount;

		// Count the outputs that have the same value as our transactionOutput.
		var equalValueWalletVirtualOutputCount = walletVirtualOutputs.Count(o => o.Amount == amount);
		var equalValueForeignRelevantVirtualOutputCount = foreignVirtualOutputs.Count(o => o.Amount == amount);

		// The anonymity set should increase by the number of equal-valued foreign outputs.
		// If we have multiple equal-valued wallet outputs, then we divide the increase evenly between them.
		// The rationale behind this is that picking randomly an output would make our anonset:
		// total/ours = 1 + foreign/ours, so the increase in anonymity is foreign/ours.
		return (double)equalValueForeignRelevantVirtualOutputCount / equalValueWalletVirtualOutputCount;
	}
}
