using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;
using System.Text;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Payload
{
	public Payload(Header header, Body body)
	{
		Header = header;
		Body = body;
	}

	[JsonProperty(PropertyName = "header")]
	public Header Header { get; }

	[JsonProperty(PropertyName = "body")]
	public Body Body { get; }

	public byte[] GetCanonicalSerialization()
	{
		return Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(this, CanonicalJsonSerializationOptions.Settings));
	}
}
