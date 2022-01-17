using Newtonsoft.Json;

namespace WalletWasabi.WebClients.Bitstamp;

public class BitstampExchangeRate
{
	[JsonProperty(PropertyName = "bid")]
	public decimal Rate { get; set; }
}
