using NBitcoin;

namespace WalletWasabi.Discoverability;

public record DiscoveredCoordinator(
	string PubKey,
	string Name,
	string Description,
	Network Network,
	Uri CoordinatorUri,
	int AbsoluteMinInputCount,
	Uri? ReadMoreUri,
	DateTimeOffset CreatedAt,
	int RelayCount);
