using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BranchNBound
{
	public class BranchAndBound
	{
		private enum NextAction
		{
			AandB,
			BandA,
			A,
			B,
			Backtrack
		}

		private readonly Random _random = new();

		public BranchAndBound(List<Money> availableCoins)
		{
			Count = availableCoins.Count;
			SortedUTXOs = availableCoins.OrderByDescending(x => x.Satoshi).Select(c => c.Satoshi).ToArray();
		}

		private long[] SortedUTXOs { get; }
		private int Count { get; }

		public bool TryGetExactMatch(Money target, [NotNullWhen(true)] out List<Money> selectedCoins)
		{
			selectedCoins = new List<Money>();

			if (SortedUTXOs.Sum() < target)
			{
				return false;
			}

			try
			{
				if (Search(target.Satoshi, out long[] solution))
				{
					selectedCoins = solution.Where(c => c > 0).Select(c => Money.Satoshis(c)).ToList();

					if (CalcEffectiveValue(selectedCoins) == target)
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

		public Money CalcEffectiveValue(List<Money> list)
		{
			Guard.NotNull(nameof(list), list);

			Money sum = Money.Satoshis(0);

			foreach (var item in list)
			{
				sum += item.Satoshi;        // TODO: effectiveValue = utxo.value − feePerByte × bytesPerInput
			}

			return sum;
		}

		private bool Search(long target, [NotNullWhen(true)] out long[] solution)
		{
			// Current effective value.
			long effValue = 0L;

			// Current depth.
			int depth = 0;

			solution = new long[Count];
			NextAction[] actions = new NextAction[Count];
			actions[0] = GetRandomNextAction();

			do
			{
				NextAction step = actions[depth];

				// Branch WITH the UTXO included.
				if ((step == NextAction.AandB) || (step == NextAction.A))
				{
					actions[depth] = GetNextStep(step);

					solution[depth] = SortedUTXOs[depth];
					effValue += SortedUTXOs[depth];

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
				else if ((step == NextAction.BandA) || (step == NextAction.B))
				{
					actions[depth] = GetNextStep(step);

					// Branch WITHOUT the UTXO included.
					effValue -= solution[depth];
					solution[depth] = 0L;

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
					effValue -= solution[depth];
					solution[depth] = 0;
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

		private NextAction GetNextStep(NextAction step)
		{
			return step switch
			{
				NextAction.AandB => NextAction.B,
				NextAction.BandA => NextAction.A,
				NextAction.A => NextAction.Backtrack,
				NextAction.B => NextAction.Backtrack,
				NextAction.Backtrack => throw new InvalidOperationException("This should never happen."),
				_ => throw new InvalidOperationException("No other values are valid.")
			};
		}
	}
}
