using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyCache : IEnumerable<HdPubKeyInfo>
{
	private readonly Dictionary<Script, HdPubKey> _hdPubKeyIndexedByScriptPubKey = new(1_000);
	private HdPubKeyInfo[] _hdPubKeyInfos = new HdPubKeyInfo[1_000];
	private int _hdKeyCount = 0;

	private ArraySegment<HdPubKeyInfo> Snapshot =>
		new(_hdPubKeyInfos, 0, _hdKeyCount);

	public IEnumerable<HdPubKey> HdPubKeys =>
		Snapshot.Select(x => x.HdPubKey);

	public bool TryGetPubKey(Script destination, [NotNullWhen(true)] out HdPubKey? hdPubKey) =>
		_hdPubKeyIndexedByScriptPubKey.TryGetValue(destination, out hdPubKey);

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
		if (_hdPubKeyInfos.Length == _hdKeyCount)
		{
			Array.Resize(ref _hdPubKeyInfos, 2 * _hdKeyCount);
		}

		var info = new HdPubKeyInfo(hdPubKey, scriptPubKeyType);
		_hdPubKeyInfos[_hdKeyCount] = info;
		_hdPubKeyIndexedByScriptPubKey[info.ScriptPubKey] = info.HdPubKey;
		_hdKeyCount++;
	}

	public IEnumerator<HdPubKeyInfo> GetEnumerator() =>
		Snapshot
			.OrderBy(x => x.HdPubKey.Index)
			.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();
}
