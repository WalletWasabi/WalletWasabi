using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models
{
	public class PublicNonceWithIndex
	{
		[JsonProperty]
		public int N { get; set; }
		
		[JsonProperty]
		[JsonConverter(typeof(PubKeyJsonConverter))]
		public PubKey R { get; set; }

		public PublicNonceWithIndex(int n, PubKey rPubKey)
		{
			N = n;
			R = rPubKey;
		}
	}
}
