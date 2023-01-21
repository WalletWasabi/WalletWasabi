using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Bridge;

public interface IAddress
{
	HdPubKey HdPubKey { get; }
	Network Network { get; }
	HDFingerprint HdFingerprint { get; }
}
