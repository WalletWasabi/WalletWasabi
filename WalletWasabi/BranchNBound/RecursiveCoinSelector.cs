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
	public class RecursiveCoinSelector : Selector
	{
		public Money Tolerance { get; private set; } = 0UL;

		private bool _optimizedCoinsFound = false;

		private List<Money>? FinalCoins { get; set; }

		public bool TryBranchAndBound(List<Money> coins, Money target, ulong maxTolerance, ulong toleranceIncrement, out List<Money> finalCoins)
		{
			var coinsAscending = coins.OrderBy(x => x);
			finalCoins = new List<Money>();
			Tolerance = 0UL;
			var currentCoins = new List<Money>();

			try
			{
				while (Tolerance <= maxTolerance)
				{
					Stack<Money> pool = new Stack<Money>(coinsAscending);
					finalCoins = SearchForCoins(currentCoins, target, Tolerance, pool);
					if (finalCoins.Any())
					{
						break;
					}
					Tolerance += toleranceIncrement;
				}
			}
			catch (IndexOutOfRangeException exc)
			{
				Logger.LogError(exc);
			}

			return finalCoins.Any();
		}

		private List<Money> SearchForCoins(List<Money> currentCoins, Money target, ulong tolerance, Stack<Money> pool)
		{
			// currentCoins : List of coins that have been choosen from the pool for optimal value
			// pool : All coins available
			// target : ulong, the value that the sum of currentCoins needs to match with
			// tolerance : the maximum difference we allow for a match
			// sum : Overall value of the coins

			while (!_optimizedCoinsFound)
			{
				// Add the top coin of the pool to the selection and calculate SUM
				if (pool.Count <= 0)
				{
					if (currentCoins.Count > 0)
					{
						currentCoins.RemoveLast();
					}
					return currentCoins;
				}
				currentCoins.Add(pool.Pop());
				var sum = CalcEffectiveValue(currentCoins);

				if ((sum >= target) && (sum <= target + tolerance))
				{
					// If its a match, we are happy
					_optimizedCoinsFound = true;
					FinalCoins = currentCoins;
				}
				else if (sum < target + tolerance)
				{
					// If the SUM is less that the target, we go forward to add coins
					SearchForCoins(currentCoins, target, tolerance, new Stack<Money>(pool.Reverse()));
				}
				if (sum > target + tolerance)
				{
					// If the SUM is bigger than the target, we remove the last added element and go forward
					currentCoins.RemoveLast();
					return SearchForCoins(currentCoins, target, tolerance, pool);
				}
			}
			return FinalCoins;
		}
	}
}
