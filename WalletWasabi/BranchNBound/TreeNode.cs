using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.BranchNBound
{
	public class TreeNode
	{
		public TreeNode()
		{
			Coins = new List<ulong>();
			Value = 0;
		}

		public TreeNode(IEnumerable<ulong> coins)
		{
			Coins = coins.ToList();
			Value = Sum(Coins);
		}

		public TreeNode(IEnumerable<ulong> coins, ulong coin)
		{
			Coins = coins.ToList();
			Coins.Add(coin);
			Value = Sum(Coins);
		}

		public ulong Value { get; set; }
		public List<ulong> Coins { get; set; }

		internal ulong Sum(IEnumerable<ulong> coins)
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
