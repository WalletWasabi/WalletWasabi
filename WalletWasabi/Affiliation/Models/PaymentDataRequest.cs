using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;
using WalletWasabi.Affiliation.Models.PaymentData;

namespace WalletWasabi.Affiliation.Models;

public record PaymentDataRequest
{
	public PaymentDataRequest(Body body, byte[] signature)
	{
		Body = body;
		Signature = signature;
	}

	[JsonProperty(PropertyName = "signature")]
	[JsonConverter(typeof(ByteArrayJsonConverter))]
	public byte[] Signature { get; }

	[JsonProperty(PropertyName = "body")]
	public Body Body { get; }
}
