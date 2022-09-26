using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Extensions;

namespace WalletWasabi.Fluent.Helpers;

public static class KeyManagerExtensions
{
	public static IEnumerable<SmartLabel> GetChangeLabels(this KeyManager km) =>
		km.GetKeys(isInternal: true).Select(x => x.Label);

	public static IEnumerable<SmartLabel> GetReceiveLabels(this KeyManager km) =>
		km.GetKeys(isInternal: false).Select(x => x.Label);

	public static int CountConsecutiveUnusedKeys(this KeyManager km, bool isInternal)
	{
		var view = km.GetView(isInternal, ScriptPubKeyType.Segwit);
		var usedKeyIndexes = view.UsedKeys.Select(x => x.Index).OrderBy(x => x);
		var auxPoints = usedKeyIndexes.Prepend(0).ToArray();
		return auxPoints
			.Zip(auxPoints[1..], (x, y) => y - x)
			.Take(auxPoints.Length - 1)
			.MaxOrDefault(0);
	}
}
