using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.BranchNBound
{
	public class TreeBranchCoinSelector
	{
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
	}
}
