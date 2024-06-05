using NBitcoin;

namespace WalletWasabi.Nostr;

public record NostrCoordinatorConfiguration(string Description, Uri Uri, Network Network);
