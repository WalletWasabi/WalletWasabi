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

	public IEnumerable<HdPubKey> GetKeys() =>
		HdPubKeyCache
			.Where(IsChildKeyOf(KeyPath))
			.Select(x => x.HdPubKey);


	public HdPubKey GenerateNewKey(SmartLabel label, KeyState keyState)
	{
		var relevantHdPubKeys = GetKeys().ToList();

		var path = new KeyPath(0);
		if (relevantHdPubKeys.Any())
		{
			int largestIndex = relevantHdPubKeys.Max(x => x.Index);
			var smallestMissingIndex = largestIndex;
			var present = new bool[largestIndex + 1];
			for (int i = 0; i < relevantHdPubKeys.Count; ++i)
			{
				present[relevantHdPubKeys[i].Index] = true;
			}
			for (int i = 1; i < present.Length; ++i)
			{
				if (!present[i])
				{
					smallestMissingIndex = i - 1;
					break;
				}
			}

			path = relevantHdPubKeys[smallestMissingIndex].NonHardenedKeyPath.Increment();
		}

		var fullPath = KeyPath.Derive(path);
		var pubKey = ExtPubKey.Derive(path).PubKey;

		var hdPubKey = new HdPubKey(pubKey, fullPath, label, keyState);
		HdPubKeyCache.AddKey(hdPubKey, ScriptPubKeyType.Segwit);

		return hdPubKey;
	}
	
	private static Func<HdPubKeyCacheEntry, bool> IsChildKeyOf(KeyPath keyPath) =>
		x => x.HdPubKey.FullKeyPath.Parent == keyPath;
}
