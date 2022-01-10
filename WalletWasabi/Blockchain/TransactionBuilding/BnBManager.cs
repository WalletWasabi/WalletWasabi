using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class BnBManager
{
	public static bool CanCreateChangelessTransaction(List<SmartCoin> availableCoins, long target, [NotNullWhen(true)] out List<SmartCoin>? selectedCoins)
	{
		selectedCoins = null;
		var longs = availableCoins.Select(x => x.Amount.Satoshi).ToList();
		BranchAndBound branchAndBound = new(longs);

		if (branchAndBound.TryGetExactMatch(target, out List<long>? bnbResult))
		{
			selectedCoins = new();
			foreach (var coin in availableCoins)
			{
				foreach (var item in bnbResult)
				{
					if (coin.Amount == item)
					{
						selectedCoins.Add(coin);
						bnbResult.Remove(item);
						break;
					}
				}
			}
			return true;
		}
		return false;
	}
}
