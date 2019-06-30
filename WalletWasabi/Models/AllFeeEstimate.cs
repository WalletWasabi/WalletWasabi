using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AllFeeEstimate : IEquatable<AllFeeEstimate>
	{
		[JsonProperty]
		public EstimateSmartFeeMode Type { get; }

		/// <summary>
		/// int: fee target, decimal: satoshi/bytes
		/// </summary>
		[JsonProperty]
		public Dictionary<int, int> Estimations { get; }

		[JsonConstructor]
		public AllFeeEstimate(EstimateSmartFeeMode type, IDictionary<int, int> estimations)
		{
			Type = type;
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
			_hashCode = null;
		}

		public Money GetFeeRate(int feeTarget)
		{
			int staoshiPerByte = Estimations
				.Where(x => x.Key <= feeTarget) // Where the target is still under or equals to the the requested target.
				.Last() // The last should be the largest feeTarget.
				.Value;

			return Money.Satoshis(staoshiPerByte);
		}

		#region Equality

		public override bool Equals(object obj) => obj is AllFeeEstimate && this == (AllFeeEstimate)obj;

		public bool Equals(AllFeeEstimate other) => this == other;

		private int? _hashCode;

		public override int GetHashCode()
		{
			if (!_hashCode.HasValue)
			{
				int hash = Type.GetHashCode();
				foreach (KeyValuePair<int, int> est in Estimations)
				{
					hash = hash ^ est.Key.GetHashCode() ^ est.Value.GetHashCode();
				}
				_hashCode = hash;
			}

			return _hashCode.Value;
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
