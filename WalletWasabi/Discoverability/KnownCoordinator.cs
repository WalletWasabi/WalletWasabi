using NBitcoin;

namespace WalletWasabi.Discoverability;

public record KnownCoordinator(
	string Name,
	Uri CoordinatorUri,
	string Description,
	Uri? ReadMoreUri,
	Network Network);
