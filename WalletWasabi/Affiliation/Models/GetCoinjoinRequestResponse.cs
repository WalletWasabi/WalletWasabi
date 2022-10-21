using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Models;

public class GetCoinjoinRequestResponse
{
	[JsonProperty(PropertyName = "coinjoin_request")]
	public byte[] CoinjoinRequest;

	public GetCoinjoinRequestResponse(byte[] coinjoinRequest)
	{
		CoinjoinRequest = coinjoinRequest;
	}
}
