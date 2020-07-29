using Newtonsoft.Json;

namespace WalletWasabi.WebClients.BlockchainInfo
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
