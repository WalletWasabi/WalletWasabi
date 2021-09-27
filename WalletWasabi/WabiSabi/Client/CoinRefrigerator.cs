using System;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinRefrigerator
	{
		private Dictionary<SmartCoin, DateTimeOffset> FrozenCoins { get; } = new();
		private TimeSpan FreezeTime { get; } = TimeSpan.FromSeconds(90);

		public void Freeze(IEnumerable<SmartCoin> coins)
		{
			foreach (var coin in coins)
			{
				if (!FrozenCoins.ContainsKey(coin))
				{
					FrozenCoins.TryAdd(coin, DateTimeOffset.UtcNow);
				}
				else
				{
					FrozenCoins[coin] = DateTimeOffset.UtcNow;
				}
			}
		}

		public bool IsFrozen(SmartCoin coin)
		{
			if (!FrozenCoins.TryGetValue(coin, out var starTime))
			{
				return false;
			}

			if (starTime.Add(FreezeTime) > DateTimeOffset.UtcNow)
			{
				return true;
			}

			FrozenCoins.Remove(coin);
			return false;
		}
	}
}
