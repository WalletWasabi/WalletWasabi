using HiddenWallet.Converters;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Store
{
	[JsonObject(MemberSerialization.OptIn)]
	public class CoinJoinTransaction
	{
		[JsonProperty(PropertyName = "Hex")]
		[JsonConverter(typeof(TransactionHexConverter))]
		public Transaction Transaction { get; private set; }

		[JsonProperty(PropertyName = "State")]
		[JsonConverter(typeof(StringEnumConverter))]
		public CoinJoinTransactionState State { get; set; }

		[JsonProperty(PropertyName = "DateTime")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset DateTime { get; set; }
    }
}
