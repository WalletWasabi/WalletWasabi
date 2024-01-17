using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyPathView : IEnumerable<HdPubKey>
{
	internal HdPubKeyPathView(IEnumerable<HdPubKey> hdPubKeys)
	{
		Keys = hdPubKeys;
	}

	protected IEnumerable<HdPubKey> Keys { get; }
	public IEnumerable<HdPubKey> CleanKeys => GetKeysByState(KeyState.Clean);
	public IEnumerable<HdPubKey> LockedKeys => GetKeysByState(KeyState.Locked);
	public IEnumerable<HdPubKey> UsedKeys => GetKeysByState(KeyState.Used);
	public IEnumerable<HdPubKey> UnusedKeys => Keys.Except(UsedKeys);

	private IEnumerable<HdPubKey> GetKeysByState(KeyState keyState) =>
		Keys.Where(x => x.KeyState == keyState);

	public IEnumerator<HdPubKey> GetEnumerator() =>
		Keys.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();
}
