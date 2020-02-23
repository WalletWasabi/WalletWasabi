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
		private object Lock { get; }
		private List<SmartCoin> Coins { get; set; }
		private HashSet<SmartCoin> CoinsSet { get; set; }

		private SmartLabel _labels;

		public SmartLabel Labels
		{
			get => _labels;
			private set => RaiseAndSetIfChanged(ref _labels, value);
		}

		public int Size => Coins.Count;

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

		public void Merge(Cluster clusters) => Merge(clusters.Coins);

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
					coin.Clusters = this;
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
			else if (x is null)
			{
				return false;
			}
			else
			{
				if (y is null)
				{
					return false;
				}
				else
				{
					lock (x.Lock)
					{
						lock (y.Lock)
						{
							return x.Coins.SequenceEqual(y.Coins);
						}
					}
				}
			}
		}

		public static bool operator !=(Cluster x, Cluster y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
