using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Analysis.Clustering
{
	public class Cluster : NotifyPropertyChangedBase, IEquatable<Cluster>
	{
		private SmartLabel _labels;

		public Cluster(params SmartCoin[] coins)
			: this(coins as IEnumerable<SmartCoin>)
		{
		}

		public Cluster(IEnumerable<SmartCoin> coins)
		{
			Lock = new object();
			Coins = coins.ToList();
			CoinsSet = Coins.ToHashSet();
			Labels = SmartLabel.Merge(Coins.Select(x => x.Label));
		}

		public SmartLabel Labels
		{
			get => _labels;
			private set => RaiseAndSetIfChanged(ref _labels, value);
		}

		public int Size => Coins.Count;

		private object Lock { get; }
		private List<SmartCoin> Coins { get; set; }
		private HashSet<SmartCoin> CoinsSet { get; set; }

		public void Merge(Cluster cluster) => Merge(cluster.Coins);

		public void Merge(IEnumerable<SmartCoin> coins)
		{
			lock (Lock)
			{
				var insertPosition = 0;
				foreach (var coin in coins.ToList())
				{
					if (CoinsSet.Add(coin))
					{
						Coins.Insert(insertPosition++, coin);
					}
					coin.Cluster = this;
				}
				if (insertPosition > 0) // at least one element was inserted
				{
					Labels = SmartLabel.Merge(Coins.Select(x => x.Label));
				}
			}
		}

		public IEnumerable<SmartCoin> GetCoins()
		{
			lock (Lock)
			{
				return Coins.ToList();
			}
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as Cluster);

		public bool Equals(Cluster other) => this == other;

		public override int GetHashCode()
		{
			lock (Lock)
			{
				int hash = 0;
				if (Coins is { })
				{
					foreach (var coin in Coins)
					{
						hash ^= coin.GetHashCode();
					}
				}
				return hash;
			}
		}

		public static bool operator ==(Cluster x, Cluster y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}
			else if (x is null || y is null)
			{
				return false;
			}
			else
			{
				lock (x.Lock)
				{
					lock (y.Lock)
					{
						// We lose the order here, which isn't great and may cause problems,
						// but this is also a significant perfomance gain.
						return x.CoinsSet.SetEquals(y.CoinsSet);
					}
				}
			}
		}

		public static bool operator !=(Cluster x, Cluster y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
