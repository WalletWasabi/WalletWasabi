using System.Collections.Immutable;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyGlobalView : HdPubKeyPathView
{
	public HdPubKeyGlobalView(ImmutableList<HdPubKey> hdPubKeys)
		: base(hdPubKeys)
	{
	}

	internal HdPubKeyPathView GetChildKeyOf(KeyPath keyPath) =>
		new(Keys.Where(x => x.FullKeyPath.Parent == keyPath).ToImmutableList());
}
