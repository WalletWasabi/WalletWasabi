using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.WebClients.SmartBit.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartBitExchangeRate
	{
		[JsonProperty(PropertyName = "code")]
		public string Code { get; set; }
		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }
		[JsonProperty(PropertyName = "rate")]
		public decimal Rate { get; set; }
	}
}
