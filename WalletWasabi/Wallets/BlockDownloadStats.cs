using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin.Protocol;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

public class BlockDownloadStats
{
	private double AverageLastSample { get; set; }
	private double StandardDeviationLastSample { get; set; }
	private int ConsecutiveFails { get; set; }
	private IEnumerable<BlockDl> Sample { get; set; } = Enumerable.Empty<BlockDl>();
	private List<BlockDl> History { get; } = new();
	private object Lock { get; } = new();
	
	public void AddBlockDl(IPAddress from, double msElapsed, bool success)
	{
		lock (Lock)
		{
			History.Add((new BlockDl(from, msElapsed, success)));
			Sample = History.Skip(Math.Max(0, History.Count - Const.SampleSize));
			(AverageLastSample, StandardDeviationLastSample) = 
				GetAverageStandardDerivation(Sample.Where(x => x.Success));
			ConsecutiveFails = success ? 0 : ConsecutiveFails + 1;
		}
	}

	private static (double, double) GetAverageStandardDerivation(IEnumerable<BlockDl> collection)
	{
		return MathUtils.AverageStandardDeviation(collection.Select(x => x.MsElapsed));
	}
	
	public int NodeTimeoutStrategy(int lastTimeout)
	{
		lock (Lock)
		{
			int baseTimeout;
			if (AverageLastSample > 0 && Sample.Count(x => x.Success) > Const.SampleMinSuccesses)
			{
				// Mean time spent recently to download a block including the spread
				baseTimeout = (int)Math.Round(AverageLastSample + Math.Max(Const.MinimumSpread, StandardDeviationLastSample * Const.StandardDerivationWeight));
			}
			else
			{
				// Same but for History if we don't have enough info in sample.
				var historyAvgSd = GetAverageStandardDerivation(History.Where(x => x.Success));
				if (historyAvgSd.Item1 > 0)
				{
					baseTimeout = (int)Math.Round(historyAvgSd.Item1 + Math.Max(Const.MinimumSpread, historyAvgSd.Item2 * Const.StandardDerivationWeight));
				}
				else
				{
					// No info at all, use default value.
					baseTimeout = Math.Max(Const.TimeoutDefaultValue, Math.Min(Const.TimeoutMaxValue, lastTimeout));
				}
			}

			var consecutiveFailsPenalty = 0.0;
			if (ConsecutiveFails > 0)
			{
				consecutiveFailsPenalty = (ConsecutiveFails - 1) * Const.BlockDlFailedPenalty *
				                          Math.Pow(Const.BlockDlFailedMultiplier, ConsecutiveFails - 1);
			}
			var result = (int)Math.Round(baseTimeout + consecutiveFailsPenalty);
			result = result > Const.TimeoutMaxValue ? Const.TimeoutMaxValue : result;
			var timeoutDeltaLog = lastTimeout >= result ? $"(-{lastTimeout - result})" : $"(+{result - lastTimeout})";
			Logger.LogDebug($"Timeout: {result}ms {timeoutDeltaLog} / Sample Avg: {Math.Round(AverageLastSample)}ms");
			return result;
		}
	}

	public bool NodeDisconnectStrategy(Node node, int connectedNodes)
	{
		lock (Lock)
		{
			// Last sample and History for blocks downloaded by this node
			var nodeDlsSample = Sample.Where(x => x.From.Equals(node.RemoteSocketAddress));
			var nodeDlsSampleCount = nodeDlsSample.Count();
			var nodeDlsHistory = History.Where(x => x.From.Equals(node.RemoteSocketAddress));
			var nodeDlsHistoryCount = nodeDlsHistory.Count();
			
			// KeepNodeScore components, the higher the score the higher probability to keep the node
			var connectedNodesScore = connectedNodes >= Const.ConnectedNodesGoal
				? (1 - Math.Min(1, (connectedNodes - Const.ConnectedNodesGoal) * Const.TooMuchNodesScoreMultiplier)) * 100 // Low score when too much nodes
				: (((double)Const.ConnectedNodesGoal - (connectedNodes - 1)) / Const.ConnectedNodesGoal) * 100; // High score when low number of nodes
			
			// Low score if high frequency of dl from this node
			var nodeFrequencyScore = (1 - Math.Min(1, Math.Pow(nodeDlsSampleCount, 2) / Sample.Count())) * 100;

			// Low score if node tends to timeout
			var nodeHistoryTimeoutsScore = Math.Max(0.15, 1 - Math.Min(1, nodeDlsHistory.Count(x => !x.Success) * Const.NodeHistoryTimeoutsMultiplier / nodeDlsHistoryCount)) * 100;
			if (ConsecutiveFails > 1)
			{
				// Lot of consecutive fails probably means temporary bandwidth/tor issue, avoid disconnecting.
				nodeHistoryTimeoutsScore = Math.Min(80, nodeHistoryTimeoutsScore * ConsecutiveFails);
			}
			
			var keepNodeScore = Math.Round(Const.NodeFrequencyWeight * nodeFrequencyScore 
			                               + Const.ConnectedNodesWeight * connectedNodesScore 
			                               + Const.NodeHistoryTimeoutsWeight * nodeHistoryTimeoutsScore);

			var result = keepNodeScore <= InsecureRandom.Instance.GetInt(0, 101);
			var blockDlResultLog = Sample.Last().Success ? "Block dl SUCCESS" : "Block dl FAILED";
			Logger.LogDebug(result
				? $"{blockDlResultLog} - Node: Disconnect - Score: {keepNodeScore} - Nb Nodes remaining: {connectedNodes - 1}"
				: $"{blockDlResultLog} - Node: Keep - Score: {keepNodeScore} - Nb Nodes: {connectedNodes}");
			return result;
		}
	}
	
	private class BlockDl
	{
		public BlockDl(IPAddress from, double msElapsed, bool success)
		{
			From = from;
			MsElapsed = msElapsed;
			Success = success;
		}
		
		public IPAddress From { get; }
		public double MsElapsed { get; }
		public bool Success { get; }
	}
	
	private static class Const 
	{
		// Sample parameters
		public static int SampleSize => 10;
		public static int SampleMinSuccesses => 2;
		
		// Node timeout strategy parameters
		public static int BlockDlFailedPenalty => 500;
		public static double BlockDlFailedMultiplier => 1.5;
		public static double StandardDerivationWeight => 2;
		public static int MinimumSpread => 2000;
		public static int TimeoutDefaultValue => 5000;
		public static int TimeoutMaxValue => 256000;
		
		// Node disconnect strategy parameters
		public static int ConnectedNodesGoal => 4;
		public static double TooMuchNodesScoreMultiplier => 0.2;
		public static double ConnectedNodesWeight => 0.25;
		
		public static double NodeFrequencyWeight => 0.1;
		
		public static double NodeHistoryTimeoutsMultiplier => 2;
		public static double NodeHistoryTimeoutsWeight => 0.65;
	}
}
