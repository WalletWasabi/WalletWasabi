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
		private List<ulong> FinalCoins { get; set; }
		private bool _optimizedCoinsFound = false;

		public bool TryBranchAndBound(List<ulong> coins, ulong target, ulong tolerance, out List<ulong> finalCoins)
		{
			var coinsDescending = coins.OrderBy(x => x);
			finalCoins = new List<ulong>();

			Stack<ulong> pool = new Stack<ulong>(coinsDescending);
			var currentCoins = new List<ulong>();
			var depth = 0;
			try
			{
				finalCoins = SolveX(currentCoins, target, pool, depth);
			}
			catch (IndexOutOfRangeException exc)
			{
				Logger.LogError(exc);
			}

			return finalCoins.Any();
		}

		private List<ulong> SolveX(List<ulong> currentCoins, ulong target, Stack<ulong> pool, int depth)
		{
			// currentCoins : List of coins that have been choosen from the pool for optimal value  || []
			// pool : All coins available															|| 12,10,10,5,4
			// target : ulong, the value that the sum of currentCoins needs to match with			|| 20
			// sum : Overall value of the coins
			Stack<ulong> tmpPool;
			while (!_optimizedCoinsFound)
			{
				// Add the top coin of the pool to the selection and calculate SUM
				if (pool.Count <= 0)
				{
					currentCoins.RemoveLast();
					return currentCoins;
				}
				currentCoins.Add(pool.Pop());
				var sum = CalculateSum(currentCoins);

				// If its a match, we are happy
				if (sum == target)
				{
					_optimizedCoinsFound = true;
					FinalCoins = currentCoins;
				}
				// If the SUM is less that the target, we go forward to add coins
				else if (sum < target)
				{
					tmpPool = new Stack<ulong>(pool.Reverse());
					SolveX(currentCoins, target, tmpPool, depth);
				}
				// If the SUM is bigger than the target, we remove the last added element and go forward
				if (sum > target)
				{
					currentCoins.RemoveLast();
					return SolveX(currentCoins, target, pool, depth);
				}
			}
			return FinalCoins;
		}

		public ulong CalculateSum(IEnumerable<ulong> coins)
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
