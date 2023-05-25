using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyCache : IEnumerable<HdPubKey>
{
	private Dictionary<Script, HdPubKey> HdPubKeysByScript { get; } = new();
	private HashSet<HdPubKey> HdPubKeys { get; } = new();
	private Dictionary<HdPubKey, byte[]> ScriptBytesByHdPubKey { get; } = new();

	private HdPubKeyGlobalView Snapshot =>
		new(this.ToImmutableList());

	public Dictionary<HdPubKey, byte[]> GetHdPubKeysWithScriptBytes() =>
		ScriptBytesByHdPubKey;

	public bool TryGetPubKey(Script destination, [NotNullWhen(true)] out HdPubKey? hdPubKey) =>
		HdPubKeysByScript.TryGetValue(destination, out hdPubKey);

	public HdPubKeyPathView GetView(KeyPath keyPath) =>
		Snapshot.GetChildKeyOf(keyPath);

	public IEnumerable<HdPubKey> AddRangeKeys(IEnumerable<HdPubKey> keys)
	{
		foreach (var key in keys)
		{
			AddKey(key, key.FullKeyPath.GetScriptTypeFromKeyPath());
		}

		return keys;
	}

	public void AddKey(HdPubKey hdPubKey, ScriptPubKeyType scriptPubKeyType)
	{
		var scriptPubKey = hdPubKey.PubKey.GetScriptPubKey(scriptPubKeyType);
		HdPubKeysByScript.AddOrReplace(scriptPubKey, hdPubKey);
		ScriptBytesByHdPubKey.AddOrReplace(hdPubKey, scriptPubKey.ToCompressedBytes());
		HdPubKeys.Add(hdPubKey);
	}

	public IEnumerator<HdPubKey> GetEnumerator() =>
		HdPubKeys
		.OrderBy(x => x.Index)
		.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();
}
