using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Bases;
using WalletWasabi.Coins;

namespace WalletWasabi.BlockchainAnalysis
{
	public class Cluster : NotifyPropertyChangedBase
	{
		private List<SmartCoin> Coins { get; set; }

		private string _labels;


		public Cluster(params SmartCoin[] coins)
			: this(coins as IEnumerable<SmartCoin>)
		{
		}

		public Cluster(IEnumerable<SmartCoin> coins)
		{
			Coins = coins.ToList();
			Labels = string.Join(", ", KnownBy);
		}

		public string Labels
		{
			get => _labels;
			private set => RaiseAndSetIfChanged(ref _labels, value);
		}

		public int Size => Coins.Count();
		
		public IEnumerable<string> KnownBy => Coins.SelectMany(x => x.Label.Labels).Distinct(StringComparer.OrdinalIgnoreCase);

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
				coin.Clusters = this;
			}
			if (insertPosition > 0) // at least one element was inserted
			{
				Labels = string.Join(", ", KnownBy);
			}
		}
	}
}
