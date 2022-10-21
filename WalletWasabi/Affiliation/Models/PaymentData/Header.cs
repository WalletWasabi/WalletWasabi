using Newtonsoft.Json;

namespace WalletWasabi.Affiliation.Models.PaymentData;

public record Header
{
	[JsonProperty(PropertyName = "title")]
	public static readonly string Title = "payment request";

	[JsonProperty(PropertyName = "version")]
	public static readonly int Version = 1;
}
