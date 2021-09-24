using System;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinRefrigerator
	{
		private Dictionary<SmartCoin, DateTimeOffset> CoinsInFridge { get; } = new();
		private TimeSpan FreezeTime { get; } = TimeSpan.FromSeconds(90);

		public void Freeze(IEnumerable<SmartCoin> coins)
		{
			foreach (var coin in coins)
			{
				if (!CoinsInFridge.ContainsKey(coin))
				{
					CoinsInFridge.TryAdd(coin, DateTimeOffset.UtcNow);
				}
				else
				{
					CoinsInFridge[coin] = DateTimeOffset.UtcNow;
				}
			}
		}

		public bool IsFrozen(SmartCoin coin)
		{
			if (!CoinsInFridge.TryGetValue(coin, out var starTime))
			{
				return false;
			}

			if (starTime.Add(FreezeTime) > DateTimeOffset.UtcNow)
			{
				return true;
			}

			CoinsInFridge.Remove(coin);
			return false;
		}
	}
}
