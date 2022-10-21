using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record Header
{
	[JsonProperty(PropertyName = "title")]
	public static readonly string Title = "payment request";

	[JsonProperty(PropertyName = "version")]
	public static readonly int Version = 1;
}
