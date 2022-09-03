using System.Collections.Generic;
using System.Linq;
using NBitcoin;

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

	public IEnumerable<HdPubKey> CleanKeys => GetKeysByState(KeyState.Clean);
	public IEnumerable<HdPubKey> LockedKeys => GetKeysByState(KeyState.Locked);
	public IEnumerable<HdPubKey> UsedKeys => GetKeysByState(KeyState.Used);
	public IEnumerable<HdPubKey> UnusedKeys => GetKeys().Except(UsedKeys);
		
	public (KeyPath KeyPath, ExtPubKey ExtPubKey) GenerateNewKey() =>
		GenerateKeyByIndex(GetNextKeyIndex());

	public IEnumerable<(KeyPath KeyPath, ExtPubKey ExtPubKey)> AssertCleanKeysIndexed()
	{
		var unusedKeys = GetKeys().Reverse().TakeWhile(x => x.KeyState == KeyState.Clean);
		var unusedKeyCount = unusedKeys.Count();
		var missingKeys = Math.Max(MinGapLimit - unusedKeyCount, 0);
		var idx = GetNextKeyIndex();
		return Enumerable.Range(idx, missingKeys)
			.Select(GenerateKeyByIndex);
	}

	private (KeyPath, ExtPubKey) GenerateKeyByIndex(int index) =>
		(KeyPath.Derive((uint)index), ExtPubKey.Derive((uint)index));
	
	private int GetNextKeyIndex() =>
		GetKeys().Select(x => x.Index).DefaultIfEmpty(-1).Max() + 1;

	private IEnumerable<HdPubKey> GetKeysByState(KeyState keyState) =>
		GetKeys().Where(x => x.KeyState == keyState);
	
	private IEnumerable<HdPubKey> GetKeys() =>
		HdPubKeyCache
			.Where(IsChildKeyOf(KeyPath))
			.Select(x => x.HdPubKey);
	
	private static Func<HdPubKeyCacheEntry, bool> IsChildKeyOf(KeyPath keyPath) =>
		x => x.HdPubKey.FullKeyPath.Parent == keyPath;
}
