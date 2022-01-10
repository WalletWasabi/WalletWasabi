using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class ChangelessTransactionCoinSelector
{
	public static bool TryGetCoins(List<SmartCoin> availableCoins, FeeRate feeRate, long target, [NotNullWhen(true)] out List<SmartCoin>? selectedCoins)
	{
		selectedCoins = null;
		// Keys are effective values of smart coins in satoshis.
		var sortedCoins = availableCoins.OrderBy(x => x.EffectiveValue(feeRate).Satoshi);

		Dictionary<SmartCoin, long> inputs = new(sortedCoins.ToDictionary(x => x, x => x.EffectiveValue(feeRate).Satoshi));

		// Pass smart coins' effective values in ascending order.
		BranchAndBound branchAndBound = new(inputs.Values.ToList());

		if (branchAndBound.TryGetExactMatch(target, out List<long>? solution))
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

			//Debug.Assert(i == solution.Count - 1);

			return true;
		}

		return false;
	}
}
