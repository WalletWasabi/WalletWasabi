using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.Blockchain.Keys;

public record HdPubKeyGenerator(ExtPubKey ExtPubKey, KeyPath KeyPath, int MinGapLimit)
{
	public (KeyPath KeyPath, ExtPubKey ExtPubKey) GenerateNewKey(HdPubKeyPathView view) =>
		GenerateKeyByIndex(GetNextKeyIndex(view));

	public IEnumerable<(KeyPath KeyPath, ExtPubKey ExtPubKey)> AssertCleanKeysIndexed(HdPubKeyPathView view)
	{
		var unusedKeys = view.Reverse().TakeWhile(x => x.KeyState == KeyState.Clean);
		var unusedKeyCount = unusedKeys.Count();
		var missingKeys = Math.Max(MinGapLimit - unusedKeyCount, 0);
		var idx = GetNextKeyIndex(view);
		return Enumerable.Range(idx, missingKeys)
			.Select(GenerateKeyByIndex);
	}

	private (KeyPath, ExtPubKey) GenerateKeyByIndex(int index) =>
		(KeyPath.Derive((uint)index), ExtPubKey.Derive((uint)index));

	private static int GetNextKeyIndex(HdPubKeyPathView view) =>
		view.Select(x => x.Index).MaxOrDefault(-1) + 1;
}
