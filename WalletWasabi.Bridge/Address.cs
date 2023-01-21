using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Bridge;

public record Address(HdPubKey HdPubKey, Network Network, HDFingerprint HdFingerprint) : IAddress;
