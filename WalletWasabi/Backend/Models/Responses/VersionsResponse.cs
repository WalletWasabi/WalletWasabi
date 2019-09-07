using Newtonsoft.Json;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses
{
	public class VersionsResponse
	{
		public string ClientVersion { get; set; }

		// KEEP THE TYPO IN IT! Otherwise the response would not be backwards compatible.
		[JsonProperty(PropertyName = "BackenMajordVersion")]
		public string BackendMajorVersion { get; set; }

		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] LegalIssuesHash { get; set; }

		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] PrivacyPolicyHash { get; set; }

		[JsonConverter(typeof(ByteArrayJsonConverter))]
		public byte[] TermsAndConditionsHash { get; set; }
	}
}
