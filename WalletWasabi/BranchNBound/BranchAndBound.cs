using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.BranchNBound
{
	public class BranchAndBound : Selector
	{
		private Money _costOfHeader = Money.Satoshis(0);
		private Money _costPerOutput = Money.Satoshis(0);
		private int _bnbTryLimit = 5;
		private Random _random = new();

		private Money[] UtxoSorted { get; set; }

		public BranchAndBound(List<Money> utxos)
		{
			UtxoSorted = utxos.OrderByDescending(x => x.Satoshi).ToArray();
		}

		public bool TryGetExactMatch(Money target, out List<Money> selectedCoins)
		{
			selectedCoins = new List<Money>();
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
	}
}
