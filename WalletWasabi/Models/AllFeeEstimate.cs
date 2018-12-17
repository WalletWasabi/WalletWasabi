using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AllFeeEstimate
	{
		[JsonProperty]
		public EstimateSmartFeeMode Type { get; }

		/// <summary>
		/// int: fee target, decimal: satoshi/bytes
		/// </summary>
		[JsonProperty]
		public Dictionary<int, decimal> Estimations { get; }

		[JsonConstructor]
		public AllFeeEstimate(EstimateSmartFeeMode type, IDictionary<int, decimal> estimations)
		{
			Type = type;
			Guard.NotNullOrEmpty(nameof(estimations), estimations);
			Estimations = new Dictionary<int, decimal>();
			var valueSet = new HashSet<decimal>();
			// Make sure values are unique and in the correct order.
			foreach (KeyValuePair<int, decimal> estimation in estimations.OrderBy(x => x.Key))
			{
				if (valueSet.Add(estimation.Value))
				{
					Estimations.TryAdd(estimation.Key, estimation.Value);
				}
			}
		}
	}
}
