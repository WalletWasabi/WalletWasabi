using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models;

public class PublicNonceWithIndex
{
	public PublicNonceWithIndex(int n, PubKey rPubKey)
	{
		N = n;
		R = rPubKey;
	}

	[JsonProperty]
	public int N { get; set; }

	[JsonProperty]
	[JsonConverter(typeof(PubKeyJsonConverter))]
	public PubKey R { get; set; }
}
