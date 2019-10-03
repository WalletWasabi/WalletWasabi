using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Models
{
	public class CoinsView : IEnumerable<SmartCoin>
	{
		private IEnumerable<SmartCoin> Coins { get; }

		public CoinsView(IEnumerable<SmartCoin> coins)
		{
			Coins = Guard.NotNull(nameof(coins), coins);
		}

		public CoinsView UnSpent()
		{
			return new CoinsView(Coins.Where(x => x.Unspent && !x.SpentAccordingToBackend));
		}

		public CoinsView Available()
		{
			return new CoinsView(Coins.Where(x => !x.Unavailable));
		}

		public CoinsView CoinJoinInProcess()
		{
			return new CoinsView(Coins.Where(x => x.CoinJoinInProgress));
		}

		public CoinsView Confirmed()
		{
			return new CoinsView(Coins.Where(x => x.Confirmed));
		}

		public CoinsView Unconfirmed()
		{
			return new CoinsView(Coins.Where(x => !x.Confirmed));
		}

		public CoinsView AtBlockHeight(Height height)
		{
			return new CoinsView(Coins.Where(x => x.Height == height));
		}

		public CoinsView SpentBy(uint256 txid)
		{
			return new CoinsView(Coins.Where(x => x.SpenderTransactionId == txid));
		}

		public CoinsView ChildrenOf(SmartCoin coin)
		{
			return new CoinsView(Coins.Where(x => x.TransactionId == coin.SpenderTransactionId));
		}

		public CoinsView DescendantOf(SmartCoin coin)
		{
			IEnumerable<SmartCoin> Generator(SmartCoin scoin)
			{
				foreach (var child in ChildrenOf(scoin))
				{
					foreach (var childDescendant in ChildrenOf(child))
					{
						yield return childDescendant;
					}

					yield return child;
				}
			}

			return new CoinsView(Generator(coin));
		}

		public CoinsView FilterBy(Func<SmartCoin, bool> expression)
		{
			return new CoinsView(Coins.Where(expression));
		}

		public CoinsView OutPoints(IEnumerable<TxoRef> outPoints)
		{
			return new CoinsView(Coins.Where(x => outPoints.Any(y => y == x.GetOutPoint())));
		}

		public Money TotalAmount()
		{
			return Coins.Sum(x => x.Amount);
		}

		public SmartCoin[] ToArray()
		{
			return Coins.ToArray();
		}

		public IEnumerator<SmartCoin> GetEnumerator()
		{
			return Coins.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Coins.GetEnumerator();
		}
	}
}
