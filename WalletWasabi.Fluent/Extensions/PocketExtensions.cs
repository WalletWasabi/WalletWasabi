using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Extensions;

public static class PocketExtensions
{
	public static Money EffectiveSumValue(this IEnumerable<Pocket> pockets, FeeRate feeRate) =>
		pockets.Sum(pocket => pocket.EffectiveSumValue(feeRate));
}
