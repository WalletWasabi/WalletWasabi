using Newtonsoft.Json;

namespace WalletWasabi.Backend.Models.Responses
{
	public class VersionsResponse
	{
		public string ClientVersion { get; set; }

		// KEEP THE TYPO IN IT! Otherwise the response would not be backwards compatible.
		[JsonProperty(PropertyName = "BackenMajordVersion")]
		public string BackendMajorVersion { get; set; }

		public string LegalDocumentsVersion { get; set; }
	}
}
