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
		private List<SmartCoin> Coins { get; set; }

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
			Coins = coins.ToList();
			Labels = SmartLabel.Merge(Coins.Select(x => x.Label));
		}

		public void Merge(Cluster clusters) => Merge(clusters.Coins);

		public void Merge(IEnumerable<SmartCoin> coins)
		{
			var insertPosition = 0;
			foreach (var coin in coins.ToList())
			{
				if (!Coins.Contains(coin))
				{
					Coins.Insert(insertPosition++, coin);
				}
				coin.Observers = this;
			}
			if (insertPosition > 0) // at least one element was inserted
			{
				Labels = SmartLabel.Merge(Coins.Select(x => x.Label));
			}
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as Cluster);

		public bool Equals(Cluster other) => this == other;

		public override int GetHashCode()
		{
			int hash = 0;
			if (Coins != null)
			{
				foreach (var coin in Coins)
				{
					hash ^= coin.GetHashCode();
				}
			}
			return hash;
		}

		public static bool operator ==(Cluster x, Cluster y)
		{
			if (x is null)
			{
				if (y is null)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				if (y is null)
				{
					return false;
				}
				else
				{
					return x.Coins.SequenceEqual(y.Coins);
				}
			}
		}

		public static bool operator !=(Cluster x, Cluster y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
