using Newtonsoft.Json;

namespace WalletWasabi.WebClients.CoinGecko
{
	public partial class CoinGeckoExchangeRateProvider
	{
		public class CoinGeckoExchangeRate
		{
			[JsonProperty(PropertyName = "current_price")]
			public decimal Rate { get; set; }
		}
	}
}
