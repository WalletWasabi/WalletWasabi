using Newtonsoft.Json;

namespace WalletWasabi.Backend.Models.Responses;

public class VersionsResponse
{
	// KEEP THE TYPO IN IT! Otherwise the response would not be backwards compatible.
	[JsonProperty(PropertyName = "BackenMajordVersion")]
	public required string BackendMajorVersion { get; init; }

	[JsonProperty(PropertyName = "LegalDocumentsVersion")]
	public required string Ww1LegalDocumentsVersion { get; init; }

	public required string Ww2LegalDocumentsVersion { get; init; }

	public required string CommitHash { get; init; }
}
