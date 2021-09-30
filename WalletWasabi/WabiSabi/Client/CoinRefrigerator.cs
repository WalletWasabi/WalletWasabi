using System;
using System.Collections.Generic;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinRefrigerator
	{
		private Dictionary<SmartCoin, FrozenCoinData> FrozenCoins { get; } = new();

		public void Freeze(IEnumerable<SmartCoin> coins, FreezeReason reason)
		{
			var now = DateTimeOffset.Now;
			foreach (var coin in coins)
			{
				var interval = reason switch
				{
					FreezeReason.CoinJoinBroadcast => TimeSpan.FromSeconds(90),
					FreezeReason.Sending => TimeSpan.FromMinutes(5),
					_ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
				};

				FrozenCoins[coin] = new FrozenCoinData(now + interval, reason);
				Logger.LogDebug($"Coin {coin.OutPoint} is frozen because of {reason}.");
			}
		}

		public bool IsFrozen(SmartCoin coin)
		{
			if (!FrozenCoins.TryGetValue(coin, out var frozenCoinData))
			{
				return false;
			}

			if (frozenCoinData.FinishTime > DateTimeOffset.UtcNow)
			{
				return true;
			}

			FrozenCoins.Remove(coin);
			Logger.LogDebug($"Coin {coin.OutPoint} defrosted, reason was {frozenCoinData.Reason}.");
			return false;
		}

		private record FrozenCoinData(DateTimeOffset FinishTime, FreezeReason Reason);
	}
}
