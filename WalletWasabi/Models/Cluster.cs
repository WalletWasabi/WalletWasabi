using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Bases;

namespace WalletWasabi.Models
{
	public class Cluster : NotifyPropertyChangedBase
	{
		private List<SmartCoin> Coins { get; set; }

		private string _labels;

		public Cluster(SmartCoin coin)
		{
			Labels = "";
			Coins = new List<SmartCoin>() { coin };
			Labels = string.Join(", ", coin.Label.Labels);
		}

		public string Labels
		{
			get => _labels;
			private set => RaiseAndSetIfChanged(ref _labels, value);
		}

		public void Merge(Cluster clusters)
		{
			Merge(clusters.Coins);
		}

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
				Labels = string.Join(", ", Coins.SelectMany(x => x.Label.Labels).Distinct(StringComparer.OrdinalIgnoreCase));
			}
		}
	}
}
