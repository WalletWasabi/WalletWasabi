namespace WalletWasabi.WebClients.SmartBit.Models
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
