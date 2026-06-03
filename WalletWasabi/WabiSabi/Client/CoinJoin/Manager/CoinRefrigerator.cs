using System.Collections.Generic;
using System.Threading;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Manager;

public class CoinRefrigerator
{
	private static readonly TimeSpan FreezeTime = TimeSpan.FromSeconds(90);

	private readonly Lock _lock = new();
	private Dictionary<SmartCoin, DateTimeOffset> FrozenCoins { get; } = new();

	public void Freeze(IEnumerable<SmartCoin> coins)
	{
		lock (_lock)
		{
			foreach (var coin in coins)
			{
				FrozenCoins[coin] = DateTimeOffset.UtcNow;
			}
		}
	}

	public bool IsFrozen(SmartCoin coin)
	{
		lock (_lock)
		{
			if (!FrozenCoins.TryGetValue(coin, out var startTime))
			{
				return false;
			}

			if (startTime.Add(FreezeTime) > DateTimeOffset.UtcNow)
			{
				return true;
			}

			FrozenCoins.Remove(coin);
			return false;
		}
	}
}
