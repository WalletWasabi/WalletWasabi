using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Blockchain.Keys;

public class HdPubKeyManager
{
	public HdPubKeyManager(ExtPubKey extPubKey, KeyPath keyPath, HdPubKeyCache hdPubKeyCache, int minGapLimit)
	{
		ExtPubKey = extPubKey;
		KeyPath = keyPath;
		HdPubKeyCache = hdPubKeyCache;
		MinGapLimit = minGapLimit;
	}
	
	public ExtPubKey ExtPubKey { get; }
	public KeyPath KeyPath { get; }
	private HdPubKeyCache HdPubKeyCache { get; }
	public int MinGapLimit { get; }

	public ExtPubKey GenerateNewKey()
	{
		var nextIndex = (uint)GetNextKeyIndex();
		var path = new KeyPath(nextIndex);
		var extPubKey = ExtPubKey.Derive(path);
		return extPubKey;
	}

	private int GetNextKeyIndex()
	{
		var keys = GetKeys();
		return keys.Any()
			? keys.Max(x => x.Index)
			: 0;
	}

	private IEnumerable<HdPubKey> GetKeys() =>
		HdPubKeyCache
			.Where(IsChildKeyOf(KeyPath))
			.Select(x => x.HdPubKey);
	
	private static Func<HdPubKeyCacheEntry, bool> IsChildKeyOf(KeyPath keyPath) =>
		x => x.HdPubKey.FullKeyPath.Parent == keyPath;
}
