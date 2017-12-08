using HiddenWallet.Converters;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Referee
{
	[JsonObject(MemberSerialization.OptIn)]
	public class BannedUtxo
    {
		[JsonProperty(PropertyName = "Utxo")]
		[JsonConverter(typeof(OutPointConverter))]
		public OutPoint Utxo { get; set; }

		[JsonProperty(PropertyName = "TimeOfBan")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset TimeOfBan { get; set; }
	}
}
