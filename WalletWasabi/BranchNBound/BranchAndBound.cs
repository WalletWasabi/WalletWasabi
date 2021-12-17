using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BranchNBound
{
	public class BranchAndBound
	{
		private readonly Random _random = new();

		public BranchAndBound(List<SmartCoin> availableCoins)
		{
			Count = availableCoins.Count;
			SortedUTXOs = availableCoins.OrderByDescending(x => x.Amount).ToArray();
		}

		private enum NextAction
		{
			/// <summary>First try to include a coin and then try not to include the coin in the selection.</summary>
			AandB,

			/// <summary>First try NOT to include a coin and then try to include the coin in the selection.</summary>
			BandA,

			/// <summary>Include coin.</summary>
			A,

			/// <summary>Omit coin.</summary>
			B,

			/// <summary>Current selection is wrong, rolling back and trying different combination.</summary>
			Backtrack
		}

		/// <remarks>Sorted in desceding order.</remarks>
		private SmartCoin[] SortedUTXOs { get; }

		private int Count { get; }

		public bool TryGetExactMatch(Money target, [NotNullWhen(true)] out List<SmartCoin>? selectedCoins)
		{
			if (CalculateSum(SortedUTXOs) < target)
			{
				selectedCoins = null;
				return false;
			}

			selectedCoins = new List<SmartCoin>();

			try
			{
				if (Search(target.Satoshi, out SmartCoin[]? solution))
				{
					selectedCoins = solution.Where(x => x is not null).ToList();
					return true;
				}
				selectedCoins = null;
				return false;
			}
			catch (Exception ex)
			{
				Logger.LogError("Couldn't find the right pair. ", ex);
				return false;
			}
		}

		private bool Search(long target, [NotNullWhen(true)] out SmartCoin[]? solution)
		{
			// Current effective value.
			long effValue = 0L;

			// Current depth (think of the depth in the recursive algorithm sense).
			int depth = 0;

			solution = new SmartCoin[Count];
			NextAction[] actions = new NextAction[Count];
			actions[0] = GetRandomNextAction();

			do
			{
				NextAction action = actions[depth];

				// Branch WITH the UTXO included.
				if ((action == NextAction.AandB) || (action == NextAction.A))
				{
					actions[depth] = GetNextStep(action);

					solution[depth] = SortedUTXOs[depth];
					effValue = (long)CalculateSum(solution);

					if (effValue > target)
					{
						// Excessive funds, cut the branch!
						continue;
					}
					else if (effValue == target)
					{
						// Match found!
						return true;
					}
					else if (depth + 1 == Count)
					{
						// Leaf reached, no match
						continue;
					}

					depth++;
					actions[depth] = GetRandomNextAction();
				}
				else if ((action == NextAction.BandA) || (action == NextAction.B))
				{
					actions[depth] = GetNextStep(action);

					// Branch WITHOUT the UTXO included.
					solution[depth] = null;
					effValue = (long)CalculateSum(solution);

					if (depth + 1 == Count)
					{
						// Leaf reached, no match
						continue;
					}

					depth++;
					actions[depth] = GetRandomNextAction();
				}
				else
				{
					solution[depth] = null;
					effValue = (long)CalculateSum(solution);
					depth--;
				}
			}
			while (depth >= 0);

			return false;
		}

		private NextAction GetRandomNextAction()
		{
			return _random.Next(0, 2) == 1 ? NextAction.AandB : NextAction.BandA;
		}

		private NextAction GetNextStep(NextAction action)
		{
			return action switch
			{
				NextAction.AandB => NextAction.B,
				NextAction.BandA => NextAction.A,
				NextAction.A => NextAction.Backtrack,
				NextAction.B => NextAction.Backtrack,
				NextAction.Backtrack => throw new InvalidOperationException("This should never happen."),
				_ => throw new InvalidOperationException("No other values are valid.")
			};
		}

		private ulong CalculateSum(SmartCoin[] coins)
		{
			ulong sum = 0;
			foreach (SmartCoin coin in coins)
			{
				if (coin is null)
				{
					continue;
				}
				sum += coin.Amount;
			}

			return sum;
		}
	}
}
