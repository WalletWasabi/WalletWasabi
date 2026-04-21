namespace WalletWasabi.Discoverability;

public record DiscoveredCoordinator(
	string PubKey,
	string Name,
	string Description,
	Uri CoordinatorUri,
	DateTimeOffset CreatedAt);
