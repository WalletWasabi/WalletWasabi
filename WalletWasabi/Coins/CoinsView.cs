using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Coins
{
	public class CoinsView : ICoinsView
	{
		private IEnumerable<SmartCoin> Coins { get; }

		public CoinsView(IEnumerable<SmartCoin> coins)
		{
			Coins = Guard.NotNull(nameof(coins), coins);
		}

		public ICoinsView Unspent() => new CoinsView(Coins.Where(x => x.Unspent && !x.SpentAccordingToBackend));

		public ICoinsView Available() => new CoinsView(Coins.Where(x => !x.Unavailable));

		public ICoinsView CoinJoinInProcess() => new CoinsView(Coins.Where(x => x.CoinJoinInProgress));

		public ICoinsView Confirmed() => new CoinsView(Coins.Where(x => x.Confirmed));

		public ICoinsView Unconfirmed() => new CoinsView(Coins.Where(x => !x.Confirmed));

		public ICoinsView AtBlockHeight(Height height) => new CoinsView(Coins.Where(x => x.Height == height));

		public ICoinsView CreatedBy(uint256 txid) => new CoinsView(Coins.Where(x => x.TransactionId == txid));

		public ICoinsView SpentBy(uint256 txid) => new CoinsView(Coins.Where(x => x.SpenderTransactionId == txid));

		public ICoinsView ChildrenOf(SmartCoin coin) => new CoinsView(Coins.Where(x => x.TransactionId == coin.SpenderTransactionId));

		public ICoinsView DescendantOf(SmartCoin coin)
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

		public ICoinsView DescendantOfAndSelf(SmartCoin coin) => new CoinsView(DescendantOf(coin).Concat(new[] { coin }));

		public ICoinsView FilterBy(Func<SmartCoin, bool> expression) => new CoinsView(Coins.Where(expression));

		public ICoinsView OutPoints(IEnumerable<TxoRef> outPoints) => new CoinsView(Coins.Where(x => outPoints.Any(y => y == x.GetOutPoint())));

		public SmartCoin GetByOutPoint(OutPoint outpoint) => Coins.FirstOrDefault(x => x.GetOutPoint() == outpoint);

		public Money TotalAmount() => Coins.Sum(x => x.Amount);

		public SmartCoin[] ToArray() => Coins.ToArray();

		public IEnumerator<SmartCoin> GetEnumerator() => Coins.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => Coins.GetEnumerator();
	}
}
