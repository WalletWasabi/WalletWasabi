using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;

namespace WalletWasabi.Affiliation.Models;

public record GetCoinjoinRequestRequest
{
	public GetCoinjoinRequestRequest(Body body, byte[] signature)
	{
		Body = body;
		Signature = signature;
	}

	[JsonProperty(PropertyName = "signature")]
	[JsonConverter(typeof(AffiliationByteArrayJsonConverter))]
	public byte[] Signature { get; }

	[JsonProperty(PropertyName = "body")]
	public Body Body { get; }
}
