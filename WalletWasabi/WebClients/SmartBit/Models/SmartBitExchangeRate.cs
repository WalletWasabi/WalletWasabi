using Newtonsoft.Json;

namespace WalletWasabi.WebClients.SmartBit.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class SmartBitExchangeRate
	{
		[JsonProperty(PropertyName = nameof(Code))]
		public string Code { get; set; }

		[JsonProperty(PropertyName = nameof(Name))]
		public string Name { get; set; }

		[JsonProperty(PropertyName = nameof(Rate))]
		public decimal Rate { get; set; }
	}
}
