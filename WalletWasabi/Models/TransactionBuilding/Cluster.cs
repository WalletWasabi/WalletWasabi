
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
			Coins = new List<SmartCoin>();
			Coins.Add(coin);
			Labels = string.Join(", ", coin.Label.Labels);
		}

		public string Labels
		{
			get => _labels;
			private set => RaiseAndSetIfChanged(ref _labels, value);
		}

		internal void Merge(Cluster clusters)
		{
			var insertPosition = 0;
			foreach (var coin in clusters.Coins.ToList())
			{
				if (!Coins.Contains(coin))
				{
					Coins.Insert(insertPosition++, coin);
				}
			}
			Labels = string.Join(", ", Coins.SelectMany(x => x.Label.Labels).Distinct(StringComparer.OrdinalIgnoreCase));
		}
	}
}