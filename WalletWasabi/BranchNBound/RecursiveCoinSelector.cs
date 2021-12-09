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
	public class RecursiveCoinSelector
	{
		private bool _optimizedCoinsFound = false;

		private Money _costOfHeader = Money.Satoshis(0);
		private Money _costPerOutput = Money.Satoshis(0);
		private int _bnbTryLimit = 5;
		private Random _random = new();
		private List<Money>? FinalCoins { get; set; }
		private Money[]? UtxoSorted { get; set; }

		public bool TryBranchAndBound(List<Money> coins, Money target, ulong maxTolerance, ulong toleranceIncrement, out ulong tolerance, out List<Money> finalCoins)
		{
			var coinsDescending = coins.OrderBy(x => x);
			finalCoins = new List<Money>();
			tolerance = 0;
			var currentCoins = new List<Money>();

			try
			{
				while (tolerance <= maxTolerance)
				{
					Stack<Money> pool = new Stack<Money>(coinsDescending);
					finalCoins = SearchForCoins(currentCoins, target, tolerance, pool);
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

		private List<Money> SearchForCoins(List<Money> currentCoins, Money target, ulong tolerance, Stack<Money> pool)
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

		internal bool TryTreeLogic(List<Money> availableCoins, Money target, out List<Money> selectedCoins)
		{
			var coinArray = availableCoins.ToArray();
			var state = new TreeNode[1] { new TreeNode() };
			selectedCoins = new List<Money>();
			var depth = 0;
			List<TreeNode> tmp;

			while (true)
			{
				tmp = new();
				foreach (var currentNode in state)
				{
					List<Money> currentCoins = currentNode.Coins;

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
				if (depth >= coinArray.Length)
				{
					return false;
				}
			}
		}

		private bool TryAddOrReturn(TreeNode node, Money target, List<TreeNode> tmp)
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

		public bool TryGetExactMatch(Money target, List<Money> availableCoins, out List<Money> selectedCoins)
		{
			selectedCoins = new List<Money>();
			UtxoSorted = availableCoins.OrderByDescending(x => x.Satoshi).ToArray();
			try
			{
				for (int i = 0; i < _bnbTryLimit; i++)
				{
					selectedCoins = RecursiveSearch(depth: 0, currentSelection: new List<Money>(), effValue: 0, target: target);
					if (CalcEffectiveValue(selectedCoins) == target + _costOfHeader + _costPerOutput)
					{
						return true;
					}
				}

				return false;
			}
			catch (Exception ex)
			{
				Logger.LogError("Couldn't find the right pair. " + ex);
				return false;
			}
		}

		private List<Money>? RecursiveSearch(int depth, List<Money> currentSelection, Money effValue, Money target)
		{
			var targetForMatch = target + _costOfHeader + _costPerOutput;
			var matchRange = _costOfHeader + _costPerOutput;

			if (effValue > targetForMatch + matchRange)
			{
				return null;        // Excessive funds, cut the branch!
			}
			else if (effValue >= targetForMatch)
			{
				return currentSelection;        // Match found!
			}
			else if (depth >= UtxoSorted.Length)
			{
				return null;        // Leaf reached, no match
			}
			else
			{
				if (_random.Next(0, 2) == 1)
				{
					var clonedSelection = currentSelection.ToList();
					clonedSelection.Add(UtxoSorted[depth]);

					var withThis = RecursiveSearch(depth + 1, clonedSelection, effValue + UtxoSorted[depth], target);
					if (withThis != null)
					{
						return withThis;
					}
					else
					{
						var withoutThis = RecursiveSearch(depth + 1, currentSelection, effValue, target);
						if (withoutThis != null)
						{
							return withoutThis;
						}

						return null;
					}
				}
				else
				{
					var withoutThis = RecursiveSearch(depth + 1, currentSelection, effValue, target);
					if (withoutThis != null)
					{
						return withoutThis;
					}
					else
					{
						var clonedSelection = currentSelection.ToList();
						clonedSelection.Add(UtxoSorted[depth]);

						var withThis = RecursiveSearch(depth + 1, clonedSelection, effValue + UtxoSorted[depth], target);
						if (withThis != null)
						{
							return withThis;
						}

						return null;
					}
				}
			}
		}

		private Money CalcEffectiveValue(List<Money> list)
		{
			Money sum = Money.Satoshis(0);

			foreach (var item in list)
			{
				sum += item.Satoshi;        // TODO: effectiveValue = utxo.value − feePerByte × bytesPerInput
			}

			return sum;
		}

		public ulong CalculateSum(IEnumerable<Money> coins)
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
