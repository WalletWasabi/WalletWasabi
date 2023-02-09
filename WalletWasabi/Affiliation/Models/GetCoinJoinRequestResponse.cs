using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Models;

public class GetCoinJoinRequestResponse
{
	[JsonProperty(PropertyName = "coinjoin_request")]
	public byte[] CoinJoinRequest { get; }

	public GetCoinJoinRequestResponse(byte[] coinJoinRequest)
	{
		CoinJoinRequest = coinJoinRequest;
	}
}
