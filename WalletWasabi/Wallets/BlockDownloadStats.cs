using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin.Protocol;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

public class BlockDownloadStats
{
	public int SampleSize { get; set; } = 20;
	private double AverageLastSample { get; set; }
	private double StandardDeviationLastSample { get; set; }
	private List<BlockDl> History { get; } = new();
	private object Lock = new();
	
	public void AddBlockDl(IPAddress from, double msElapsed, bool success)
	{
		lock (Lock)
		{
			History.Add((new BlockDl(from, msElapsed, success)));
			var lastSampleSuccessTime = GetSample().Where(x => x.Success).Select(x => x.MsElapsed);
			(AverageLastSample, StandardDeviationLastSample) = Helpers.MathUtils.AverageStandardDeviation(lastSampleSuccessTime);
		}
	}

	private (double, double) GetAvgSdForSuccessHistory()
	{
		return Helpers.MathUtils.AverageStandardDeviation(History.Where(x => x.Success).Select(x => x.MsElapsed));
	}

	private IEnumerable<BlockDl> GetSample()
	{
		return History.Skip(Math.Max(0, History.Count - SampleSize));
	}

	public int NodeTimeoutStrategy(int currentTimeout, int failsPenaltyMs = 2000)
	{
		lock (Lock)
		{
			var sample = GetSample();

			int baseTimeout;
			if (AverageLastSample > 0 && sample.Count(x => x.Success) > 4)
			{
				// Mean time spent recently to download a block including the spread
				baseTimeout = (int)Math.Round(AverageLastSample + Math.Max(2000, StandardDeviationLastSample * 2));
			}
			else
			{
				// Same but for History if we don't have enough info in sample.
				var historyAvgSd = GetAvgSdForSuccessHistory();
				if (historyAvgSd.Item1 > 0)
				{
					baseTimeout = (int)Math.Round(historyAvgSd.Item1 + Math.Max(2000, historyAvgSd.Item2 * 2));
				}
				else
				{
					// No info at all, use reset value.
					baseTimeout = Math.Max(5000, Math.Min(128000, currentTimeout));
				}
			}

			var nbLastFails = sample.Reverse().TakeWhile(x => !x.Success).Count();
			var result = (int)Math.Round((double)baseTimeout + nbLastFails * failsPenaltyMs);
			Logger.LogTrace($"NodeTimeout: {result}");
			return result;
		}
	}

	public bool NodeDisconnectStrategy(Node node, int connectedNodes, int nbNodesGoal = 4)
	{
		lock (Lock)
		{
			// Last sample and History for blocks downloaded by this node
			var nodeLastDls = GetSample().Where(x => x.From.Equals(node.RemoteSocketAddress));
			var nodeLastDlsCount = nodeLastDls.Count();
			var nodeDlsHistory = History.Where(x => x.From.Equals(node.RemoteSocketAddress));
			var nodeDlsHistoryCount = nodeDlsHistory.Count();

			// Low score if high frequency of dl from this node
			var coeffSameNode = 0.05;
			var scoreSameNode = (1 - (double)nodeLastDlsCount / GetSample().Count()) * 100;

			var coeffNbNodes = 0.15;
			var scoreNbNodes = connectedNodes >= nbNodesGoal
				? (1 - Math.Min(1, (connectedNodes - nbNodesGoal) * 0.2)) * 100 // Low score when too much nodes
				: (((double)nbNodesGoal - (connectedNodes - 1)) / nbNodesGoal) *
				  100; // High score when low number of nodes

			// Low score if node tends to fail
			var coeffFails = 0.8;
			var scoreFails =
				(1 - Math.Min(1, ((double)nodeDlsHistory.Count(x => !x.Success) * 2 / nodeDlsHistoryCount))) * 100;

			var keepNodeScore =
				Math.Round(coeffSameNode * scoreSameNode + coeffFails * scoreFails + coeffNbNodes * scoreNbNodes);

			var result = keepNodeScore <= InsecureRandom.Instance.GetInt(0, 101);
			var resultLog = result ? "disconnect" : "keep";
			Logger.LogTrace($"{resultLog} node with score: {keepNodeScore}");
			return result;
		}
	}
	
	public class BlockDl
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
}
