using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public record HdPubKeyEx(HdPubKey HdPubKey, byte[] ScriptPubKeyBytes);

public class HdPubKeyRegistry : IEnumerable<HdPubKeyEx>
{
	private Dictionary<Script, HdPubKeyEx> KeyExtendedDataByScript { get; } = new ();

	public void AddKey(HdPubKey hdPubKey, ScriptPubKeyType scriptPubKeyType)
	{
		var scriptPubKey = hdPubKey.PubKey.GetScriptPubKey(scriptPubKeyType);
		KeyExtendedDataByScript.AddOrReplace(scriptPubKey, new HdPubKeyEx(hdPubKey, scriptPubKey.ToCompressedBytes()));
	}

	public bool TryGetPubkey(Script destination, [NotNullWhen(true)] out HdPubKeyEx? hdPubKeyEx) =>
		KeyExtendedDataByScript.TryGetValue(destination, out hdPubKeyEx);

	public void AddRangeKeys(IEnumerable<HdPubKey> keys)
	{
		foreach (var key in keys)
		{
			AddKey(key, ScriptPubKeyType.Segwit);
		}
	}

	public IEnumerator<HdPubKeyEx> GetEnumerator() =>
		KeyExtendedDataByScript.Values.DistinctBy(x => x.HdPubKey).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();
}
