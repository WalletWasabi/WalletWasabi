using Newtonsoft.Json;

namespace WalletWasabi.WebClients.BlockchainInfo
{
	public partial class CoinstampExchangeRateProvider
	{
		public class CoinstampExchangeRate
		{
			[JsonProperty(PropertyName = "bid")]
			public decimal Rate { get; set; }
		}
	}
}
