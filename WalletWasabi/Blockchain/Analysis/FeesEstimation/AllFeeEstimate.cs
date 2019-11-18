using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AllFeeEstimate : IEquatable<AllFeeEstimate>
	{
		[JsonProperty]
		public EstimateSmartFeeMode Type { get; private set; }

		/// <summary>
		/// If it's been fetched from a fully synced node.
		/// </summary>
		[JsonProperty]
		public bool IsAccurate { get; private set; }

		/// <summary>
		/// int: fee target, decimal: satoshi/byte
		/// </summary>
		[JsonProperty]
		public Dictionary<int, int> Estimations { get; private set; }

		[JsonConstructor]
		public AllFeeEstimate(EstimateSmartFeeMode type, IDictionary<int, int> estimations, bool isAccurate)
		{
			Create(type, estimations, isAccurate);
		}

		public AllFeeEstimate(AllFeeEstimate other, bool isAccurate)
		{
			Create(other.Type, other.Estimations, isAccurate);
		}

		private void Create(EstimateSmartFeeMode type, IDictionary<int, int> estimations, bool isAccurate)
		{
			Type = type;
			IsAccurate = isAccurate;
			Guard.NotNullOrEmpty(nameof(estimations), estimations);
			Estimations = new Dictionary<int, int>();
			var valueSet = new HashSet<decimal>();
			// Make sure values are unique and in the correct order.
			foreach (KeyValuePair<int, int> estimation in estimations.OrderBy(x => x.Key))
			{
				if (valueSet.Add(estimation.Value))
				{
					Estimations.TryAdd(estimation.Key, estimation.Value);
				}
			}
		}

		public FeeRate GetFeeRate(int feeTarget)
		{
			// Where the target is still under or equal to the requested target.
			decimal satoshiPerByte = Estimations
				.Last(x => x.Key <= feeTarget) // The last should be the largest feeTarget.
				.Value;

			return new FeeRate(satoshiPerByte);
		}

		#region Equality

		public override bool Equals(object obj) => obj is AllFeeEstimate feeEstimate && this == feeEstimate;

		public bool Equals(AllFeeEstimate other) => this == other;

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

		public static bool operator ==(AllFeeEstimate x, AllFeeEstimate y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (x is null ^ y is null)
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

		public static bool operator !=(AllFeeEstimate x, AllFeeEstimate y) => !(x == y);

		#endregion Equality
	}
}
