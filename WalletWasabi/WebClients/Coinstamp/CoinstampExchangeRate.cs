using Newtonsoft.Json;

namespace WalletWasabi.WebClients.Coinstamp
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
