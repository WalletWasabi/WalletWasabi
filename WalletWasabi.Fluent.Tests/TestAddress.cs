using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Bridge;

namespace WalletWasabi.Fluent.Tests;

public record TestAddress : IAddress
{
	public HdPubKey HdPubKey { get; }
	public Network Network { get; }
	public HDFingerprint HdFingerprint { get; }
}
