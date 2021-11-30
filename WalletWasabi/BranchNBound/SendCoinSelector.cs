using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Logging;

namespace WalletWasabi.BranchNBound
{
	public class SendCoinSelector
	{
		public bool TryBranchAndBound(List<ulong> coins, ulong target, ulong tolerance, out List<ulong> selectedCoins)
		{
			selectedCoins = new List<ulong>();
			var bnbTries = 10000;
			try
			{
				selectedCoins = SolveX(coins, target, tolerance);
			}
			catch (InvalidOperationException exc)
			{
				Logger.LogError(exc);
			}

			return selectedCoins.Any();
		}

		private List<ulong> SolveX(List<ulong> coins, ulong target, ulong tolerance)
		{
			var coinsDesc = new Stack<ulong>(coins.OrderBy(x => x));
			var selection = new Queue<ulong>();

			while (true)
			{
				var totalSelected = CalculateSum(selection); //Get SUM of coins in satoshis (ulong)

				if (totalSelected < target && coins.Any())
				{
					selection.Enqueue(coinsDesc.Pop()); //Not enough coins selected
				}
				else if (totalSelected >= target && totalSelected <= target + tolerance)
				{
					return selection.ToList(); // Match found with set tolerance
				}
				else if (totalSelected > target + tolerance && selection.Any())
				{
					selection.Dequeue(); // SUM of coins is bigger than the target
				}
				else
				{
					throw new InvalidOperationException($"It was not possible to find a set of coins to meet the required {target} amount.");
				}
			}
		}

		private ulong CalculateSum(IEnumerable<ulong> coins)
		{
			ulong sum = 0;
			foreach (var coin in coins)
			{
				sum += coin;
			}

			return sum;
		}
	}
}
