using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyCache : IEnumerable<HdPubKeyInfo>
{
	private Dictionary<Script, HdPubKey> HdPubKeyIndexedByScriptPubKey { get; } = new(1_000);
	private List<HdPubKeyInfo> HdPubKeyInfos { get; } = new(1_000);
	private List<HdPubKeyInfo>? HdPubKeyInfoSnapshot { get; set; }

	public IEnumerable<HdPubKey> HdPubKeys =>
		this.Select(x => x.HdPubKey);

	public bool TryGetPubKey(Script destination, [NotNullWhen(true)] out HdPubKey? hdPubKey) =>
		HdPubKeyIndexedByScriptPubKey.TryGetValue(destination, out hdPubKey);

	public HdPubKeyPathView GetView(KeyPath keyPath) =>
		new(HdPubKeys.Where(x => x.FullKeyPath.Parent == keyPath));

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
		var info = new HdPubKeyInfo(hdPubKey, scriptPubKeyType);
		HdPubKeyInfos.Add(info);
		HdPubKeyIndexedByScriptPubKey[info.ScriptPubKey] = info.HdPubKey;
	}

	public IEnumerator<HdPubKeyInfo> GetEnumerator()
	{
		if (HdPubKeyInfoSnapshot is not { } snapshot || snapshot.Count != HdPubKeyInfos.Count)
		{
			HdPubKeyInfoSnapshot = HdPubKeyInfos.ToList();
		}
		return HdPubKeyInfoSnapshot.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();
}
