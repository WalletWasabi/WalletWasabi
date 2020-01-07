using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Logging;

namespace WalletWasabi.CoinJoin.Coordinator.Dynamic
{
	public class CoinJoinStatistics
	{
		private List<decimal> AverageWcqs { get; }
		private DateTimeOffset LatestMeasurementStart { get; set; }
		public TimeoutAdjustment LastTimeoutAdjustment { get; set; }
		private List<decimal> CurrentWcqs { get; }
		private object Lock { get; }
		public TimeSpan Interval { get; }

		public CoinJoinStatistics(TimeSpan interval)
		{
			AverageWcqs = new List<decimal>();
			LatestMeasurementStart = DateTimeOffset.UtcNow;
			CurrentWcqs = new List<decimal>();
			Lock = new object();
			Interval = interval;
			LastTimeoutAdjustment = TimeoutAdjustment.Up;
		}

		public IEnumerable<decimal> GetAverageWcqs()
		{
			lock (Lock)
			{
				return AverageWcqs.ToList();
			}
		}

		/// <returns>If new interval is triggered.</returns>
		public bool Register(Transaction coinjoin)
		{
			var newIntervalTriggered = false;
			var quality = coinjoin.CalculateWasabiCoinJoinQuality();
			lock (Lock)
			{
				var diff = DateTimeOffset.UtcNow - LatestMeasurementStart;
				if (diff > Interval)
				{
					newIntervalTriggered = true;
					var average = CurrentWcqs.Average();
					Logger.LogInfo($"Average Wasabi CoinJoin Quality for the last 24 hours: {average}.");
					AverageWcqs.Add(average);
					CurrentWcqs.Clear();
					LatestMeasurementStart = DateTimeOffset.UtcNow;
				}
				else
				{
					newIntervalTriggered = false;
				}
				CurrentWcqs.Add(quality);
			}
			return newIntervalTriggered;
		}
	}
}
