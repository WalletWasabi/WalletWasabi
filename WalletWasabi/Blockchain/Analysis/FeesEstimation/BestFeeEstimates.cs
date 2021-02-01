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
	public class BestFeeEstimates : IEquatable<BestFeeEstimates>
	{
		[JsonConstructor]
		public BestFeeEstimates(EstimateSmartFeeMode type, IDictionary<int, int> estimations, bool isAccurate)
		{
			Type = type;
			IsAccurate = isAccurate;
			Guard.NotNullOrEmpty(nameof(estimations), estimations);
			Estimations = new Dictionary<int, int>();

			var orderedEstimations = estimations.OrderByDescending(x => x.Key).ToArray();
			var m20 = orderedEstimations.First(x => x.Key <= Constants.TwentyMinutesConfirmationTarget).Value;
			var m30 = orderedEstimations.First(x => x.Key <= Constants.ThirtyMinutesConfirmationTarget).Value;
			var h1 = orderedEstimations.First(x => x.Key <= Constants.OneHourConfirmationTarget).Value;
			var h3 = orderedEstimations.First(x => x.Key <= Constants.ThreeHoursConfirmationTarget).Value;
			var h6 = orderedEstimations.First(x => x.Key <= Constants.SixHoursConfirmationTarget).Value;
			var h12 = orderedEstimations.First(x => x.Key <= Constants.TwelveHoursConfirmationTarget).Value;
			var d1 = orderedEstimations.First(x => x.Key <= Constants.OneDayConfirmationTarget).Value;
			var d3 = orderedEstimations.First(x => x.Key <= Constants.ThreeDaysConfirmationTarget).Value;
			var w1 = orderedEstimations.First(x => x.Key <= Constants.SevenDaysConfirmationTarget).Value;

			// Make sure values are unique and in the correct order and feerates are consistently decreasing.
			var lastFeeRate = int.MaxValue;
			foreach (KeyValuePair<int, int> estimation in new Dictionary<int, int>
				{
					{ Constants.TwentyMinutesConfirmationTarget, m20 },
					{ Constants.ThirtyMinutesConfirmationTarget, m30 },
					{ Constants.OneHourConfirmationTarget, h1 },
					{ Constants.ThreeHoursConfirmationTarget, h3 },
					{ Constants.SixHoursConfirmationTarget, h6 },
					{ Constants.TwelveHoursConfirmationTarget, h12 },
					{ Constants.OneDayConfirmationTarget, d1 },
					{ Constants.ThreeDaysConfirmationTarget, d3 },
					{ Constants.SevenDaysConfirmationTarget, w1 }
				})
			{
				// Otherwise it's inconsistent data.
				if (lastFeeRate > estimation.Value)
				{
					lastFeeRate = estimation.Value;
					Estimations.TryAdd(estimation.Key, estimation.Value);
				}
			}
		}

		public BestFeeEstimates(BestFeeEstimates other, bool isAccurate)
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

		public override bool Equals(object? obj) => Equals(obj as BestFeeEstimates);

		public bool Equals(BestFeeEstimates? other) => this == other;

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

		public static bool operator ==(BestFeeEstimates? x, BestFeeEstimates? y)
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

		public static bool operator !=(BestFeeEstimates? x, BestFeeEstimates? y) => !(x == y);

		#endregion Equality
	}
}
