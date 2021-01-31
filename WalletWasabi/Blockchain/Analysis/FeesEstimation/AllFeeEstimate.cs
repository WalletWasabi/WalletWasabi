using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Analysis.FeesEstimation
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AllFeeEstimate : IEquatable<AllFeeEstimate>
	{
		[JsonConstructor]
		public AllFeeEstimate(EstimateSmartFeeMode type, IDictionary<int, int> estimations, bool isAccurate)
		{
			Type = type;
			IsAccurate = isAccurate;
			Guard.NotNullOrEmpty(nameof(estimations), estimations);
			Estimations = new Dictionary<int, int>();

			// Make sure values are unique and in the correct order and feerates are consistently decreasing.
			var lastFeeRate = int.MaxValue;
			foreach (KeyValuePair<int, int> estimation in estimations.OrderBy(x => x.Key))
			{
				// Otherwise it's inconsistent data.
				if (lastFeeRate > estimation.Value)
				{
					lastFeeRate = estimation.Value;
					Estimations.TryAdd(estimation.Key, estimation.Value);
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

		/// <returns>
		/// An estimates based on these estimates, but randomizes each estimation between
		/// the value of its larger estimation sibling - or 50% more if the largest
		/// with a probabilistic bias towards the estimate.
		/// </returns>
		public AllFeeEstimate? Unfingerprint()
		{
			if (Estimations is null)
			{
				return null;
			}

			var rand = new InsecureRandom();
			var fingerprintless = new Dictionary<int, int>();
			KeyValuePair<int, int>[] array = Estimations.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				KeyValuePair<int, int> current = array[i];
				var minInclusive = current.Value;
				int maxExclusive = i == 0 ? (int)(current.Value * 1.5d) : array[i - 1].Value;
				var middle = minInclusive + (maxExclusive - minInclusive) / 2;

				var bias = rand.GetInt(0, 10);
				int est;
				if (bias < 3 || minInclusive >= middle)
				{
					est = minInclusive;
				}
				else if (bias < 7)
				{
					est = rand.GetInt(minInclusive, middle);
				}
				else
				{
					est = rand.GetInt(minInclusive, maxExclusive);
				}

				fingerprintless.Add(current.Key, est);
			}
			return new AllFeeEstimate(Type, fingerprintless, IsAccurate);
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
