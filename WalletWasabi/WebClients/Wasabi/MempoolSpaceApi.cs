using System.Net.Http;

namespace WalletWasabi.WebClients.Wasabi;

public static class MempoolSpaceApi
{
	public const string ClearNetDomain = "https://mempool.space";
	public const string OnionDomain = "http://mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion";

	public static Uri GetBaseUri(IHttpClientFactory factory, Network network) => new(
		(factory is OnionHttpClientFactory ? OnionDomain : ClearNetDomain)
		+ (network == Network.Main ? "/api/" : "/testnet4/api/"));
}
