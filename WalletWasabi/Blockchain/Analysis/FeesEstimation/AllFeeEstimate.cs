using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AllFeeEstimate : IEquatable<AllFeeEstimate>
	{
		[JsonConstructor]
		public AllFeeEstimate(EstimateSmartFeeMode type, IDictionary<int, FeeRate> estimations, bool isAccurate)
		{
			Create(type, estimations, isAccurate);
		}

		public AllFeeEstimate(AllFeeEstimate other, bool isAccurate)
		{
			Create(other.Type, other.Estimations, isAccurate);
		}

		[JsonProperty]
		public EstimateSmartFeeMode Type { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the fee has been fetched from a fully synced node.
		/// </summary>
		[JsonProperty]
		public bool IsAccurate { get; private set; }

		/// <summary>
		/// Gets the fee estimations: int: fee target, int: satoshi/vByte
		/// </summary>
		[JsonProperty(ItemConverterType = typeof(FeeRatePerKbJsonConverter))]
		public Dictionary<int, FeeRate> Estimations { get; private set; }

		private void Create(EstimateSmartFeeMode type, IDictionary<int, FeeRate> estimations, bool isAccurate)
		{
			Type = type;
			IsAccurate = isAccurate;
			Guard.NotNullOrEmpty(nameof(estimations), estimations);
			Estimations = new Dictionary<int, FeeRate>();

			// Make sure values are unique and in the correct order and feerates are consistently decreasing.
			var lastFeeRate = new FeeRate(Constants.MaximumNumberOfSatoshis);
			foreach (KeyValuePair<int, FeeRate> estimation in estimations.OrderBy(x => x.Key))
			{
				// Otherwise it's inconsistent data.
				if (lastFeeRate > estimation.Value)
				{
					lastFeeRate = estimation.Value;
					Estimations.TryAdd(estimation.Key, estimation.Value);
				}
			}
		}

		public FeeRate GetFeeRate(int feeTarget)
		{
			// Where the target is still under or equal to the requested target.
			return Estimations
				.Last(x => x.Key <= feeTarget) // The last should be the largest feeTarget.
				.Value;
		}

		#region Equality

		public override bool Equals(object? obj) => Equals(obj as AllFeeEstimate);

		public bool Equals(AllFeeEstimate? other) => this == other;

		public override int GetHashCode()
		{
			var hashCode = new HashCode();
			hashCode.Add(Type);
			hashCode.Add(IsAccurate);
			foreach (KeyValuePair<int, FeeRate> est in Estimations)
			{
				hashCode.Add(est);
			}
			return hashCode.ToHashCode();
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
					if (y.Estimations.TryGetValue(pair.Key, out FeeRate value))
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
