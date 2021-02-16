using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	/// <summary>
	/// Estimates for 1w, 3d, 1d, 12h, 6h, 3h, 1h, 30m, 20m.
	/// </summary>
	[JsonObject(MemberSerialization.OptIn)]
	public class AllFeeEstimate : IEquatable<AllFeeEstimate>
	{
		[JsonConstructor]
		public AllFeeEstimate(EstimateSmartFeeMode type, IDictionary<int, int> estimations, bool isAccurate)
		{
			Guard.NotNullOrEmpty(nameof(estimations), estimations);

			Type = type;
			IsAccurate = isAccurate;

			var targets = Constants.ConfirmationTargets.Prepend(1).ToArray();
			var targetRanges = targets.Skip(1).Zip(targets, (x, y) => (Start: y, End: x));

			var filteredEstimations = estimations
				.Where(x => x.Key >= targets[0] && x.Key <= targets[^1])
				.OrderBy(x => x.Key)
				.Select(x => (ConfirmationTarget: x.Key, FeeRate: x.Value, Range: targetRanges.First(y => y.Start < x.Key && x.Key <= y.End)))
				.GroupBy(x => x.Range, y => y, (x, y) => (Range: x, BestEstimation: y.Last()))
				.Select(x => (ConfirmationTarget: x.Range.End, FeeRate: x.BestEstimation.FeeRate));

			// Make sure values are unique and in the correct order and feerates are consistently decreasing.
			Estimations = new Dictionary<int, int>();
			var lastFeeRate = int.MaxValue;
			foreach (var estimation in filteredEstimations)
			{
				// Otherwise it's inconsistent data.
				if (lastFeeRate > estimation.FeeRate)
				{
					lastFeeRate = estimation.FeeRate;
					Estimations.TryAdd(estimation.ConfirmationTarget, estimation.FeeRate);
				}
			}
		}

		public AllFeeEstimate(AllFeeEstimate other, bool isAccurate)
			: this(other.Type, other.Estimations, isAccurate)
		{
		}

		[JsonProperty]
		public EstimateSmartFeeMode Type { get; }

		/// <summary>
		/// Gets a value indicating whether the fee has been fetched from a fully synced node.
		/// </summary>
		[JsonProperty]
		public bool IsAccurate { get; }

		/// <summary>
		/// Gets the fee estimations: int: fee target, int: satoshi/vByte
		/// </summary>
		[JsonProperty]
		public Dictionary<int, int> Estimations { get; }

		public FeeRate GetFeeRate(int feeTarget)
		{
			// Where the target is still under or equal to the requested target.
			decimal satoshiPerByte = Estimations
				.Last(x => x.Key <= feeTarget) // The last should be the largest feeTarget.
				.Value;

			return new FeeRate(satoshiPerByte);
		}

		#region Equality

		public override bool Equals(object? obj) => Equals(obj as AllFeeEstimate);

		public bool Equals(AllFeeEstimate? other) => this == other;

		public override int GetHashCode()
		{
			int hash = Type.GetHashCode();
			foreach (KeyValuePair<int, int> est in Estimations)
			{
				hash ^= est.Key.GetHashCode() ^ est.Value.GetHashCode();
			}
			hash ^= IsAccurate.GetHashCode();

			return hash;
		}

		public static bool operator ==(AllFeeEstimate? x, AllFeeEstimate? y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (x is null || y is null)
			{
				return false;
			}

			if (x.Type != y.Type)
			{
				return false;
			}

			if (x.IsAccurate != y.IsAccurate)
			{
				return false;
			}

			bool equal = false;
			if (x.Estimations.Count == y.Estimations.Count) // Require equal count.
			{
				equal = true;
				foreach (var pair in x.Estimations)
				{
					if (y.Estimations.TryGetValue(pair.Key, out int value))
					{
						// Require value be equal.
						if (value != pair.Value)
						{
							equal = false;
							break;
						}
					}
					else
					{
						// Require key be present.
						equal = false;
						break;
					}
				}
			}

			return equal;
		}

		public static bool operator !=(AllFeeEstimate? x, AllFeeEstimate? y) => !(x == y);

		#endregion Equality
	}
}
