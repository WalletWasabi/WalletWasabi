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

		public bool TryBranchAndBound(List<ulong> coins, ulong target, ulong maxTolerance, ulong toleranceIncrement, out ulong tolerance, out List<ulong> finalCoins)
		{
			var coinsDescending = coins.OrderBy(x => x);
			finalCoins = new List<ulong>();
			tolerance = 0;
			var currentCoins = new List<ulong>();
			var depth = 0;
			try
			{
				while (tolerance <= maxTolerance)
				{
					Stack<ulong> pool = new Stack<ulong>(coinsDescending);
					finalCoins = SolveX(currentCoins, target, tolerance, pool, depth);
					if (finalCoins.Any())
					{
						break;
					}
					tolerance += toleranceIncrement;
				}
			}
			catch (IndexOutOfRangeException exc)
			{
				Logger.LogError(exc);
			}

			return finalCoins.Any();
		}

		private List<ulong> SolveX(List<ulong> currentCoins, ulong target, ulong tolerance, Stack<ulong> pool, int depth)
		{
			// currentCoins : List of coins that have been choosen from the pool for optimal value
			// pool : All coins available
			// target : ulong, the value that the sum of currentCoins needs to match with
			// tolerance : the maximum difference we allow for a match
			// sum : Overall value of the coins
			Stack<ulong> tmpPool;
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
				var sum = CalculateSum(currentCoins);

				// If its a match, we are happy
				if ((sum >= target) && (sum <= target + tolerance))
				{
					_optimizedCoinsFound = true;
					FinalCoins = currentCoins;
				}
				// If the SUM is less that the target, we go forward to add coins
				else if (sum < target + tolerance)
				{
					depth++;
					tmpPool = new Stack<ulong>(pool.Reverse());
					SolveX(currentCoins, target, tolerance, tmpPool, depth);
				}
				// If the SUM is bigger than the target, we remove the last added element and go forward
				if (sum > target + tolerance)
				{
					currentCoins.RemoveLast();
					return SolveX(currentCoins, target, tolerance, pool, depth);
				}
			}
			return FinalCoins;
		}

		internal bool TryTreeLogic(List<ulong> availableCoins, ulong target, out List<ulong> selectedCoins)
		{
			var coinArray = availableCoins.ToArray();
			var state = new TreeNode[1] { new TreeNode() };
			var depth = 0;
			List<TreeNode> tmp;

			while (true)
			{
				tmp = new();
				foreach (var currentNode in state)
				{
					List<ulong> currentCoins = currentNode.Coins;

					TreeNode ommit = new(currentCoins);
					if (!TryAddOrReturn(ommit, target, tmp))
					{
						selectedCoins = ommit.Coins;
						return true;
					}

					TreeNode include = new(currentCoins, coinArray[depth]);
					if (!TryAddOrReturn(include, target, tmp))
					{
						selectedCoins = include.Coins;
						return true;
					}
				}
				state = tmp.ToArray();
				depth++;
			}
			return true;
		}

		private bool TryAddOrReturn(TreeNode node, ulong target, List<TreeNode> tmp)
		{
			if (node.Value == target)
			{
				return false;
			}
			else if (node.Value < target)
			{
				tmp.Add(node);
			}
			return true;
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
