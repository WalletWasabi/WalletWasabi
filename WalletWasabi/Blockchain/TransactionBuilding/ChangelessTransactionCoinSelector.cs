using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class ChangelessTransactionCoinSelector
{
	public static bool TryGetCoins(List<SmartCoin> availableCoins, FeeRate feeRate, long target, [NotNullWhen(true)] out List<SmartCoin>? selectedCoins, CancellationToken cancellationToken)
	{
		selectedCoins = null;
		// Keys are effective values of smart coins in satoshis.
		var sortedCoins = availableCoins.OrderByDescending(x => x.EffectiveValue(feeRate).Satoshi);

		Dictionary<SmartCoin, long> inputs = new(sortedCoins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));

		// Pass smart coins' effective values in descending order.
		BranchAndBound branchAndBound = new(inputs.Values.ToList());

		if (branchAndBound.TryGetExactMatch(target, out List<long>? solution, cancellationToken))
		{
			selectedCoins = new();
			int i = 0;

			foreach ((SmartCoin smartCoin, long effectiveSatoshis) in inputs)
			{
				if (effectiveSatoshis == solution[i])
				{
					i++;
					selectedCoins.Add(smartCoin);
					if (i == solution.Count)
					{
						break;
					}
				}
			}

			return true;
		}

		return false;
	}
}
