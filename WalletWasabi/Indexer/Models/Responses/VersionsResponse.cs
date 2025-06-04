namespace WalletWasabi.Indexer.Models.Responses;

public class VersionsResponse
{
	public required string ClientVersion { get; init; }

	public required string IndexerMajorVersion { get; init; }

	public required string CommitHash { get; init; }
}
