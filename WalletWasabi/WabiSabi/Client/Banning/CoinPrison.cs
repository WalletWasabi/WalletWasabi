using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class CoinPrison(string filePath, Dictionary<OutPoint, PrisonedCoinRecord> bannedCoins) : IDisposable
{
	enum BanningStatus
	{
		Banned,
		BanningExpired,
		NonBanned
	}

	// Coins with banning time longer than this will be reduced to a random value between 2 and 4 days.
	private static readonly TimeSpan MaxDaysToTrustLocalPrison = TimeSpan.FromDays(4);

	private readonly object _lock = new();

	public void Ban(SmartCoin coin, DateTimeOffset until)
	{
		lock (_lock)
		{
			if (bannedCoins.ContainsKey(coin.Outpoint) || until < DateTimeOffset.UtcNow)
			{
				return;
			}

			var effectiveBanningTime = EffectiveBanningTime(until);
			bannedCoins.Add(coin.Outpoint, new PrisonedCoinRecord(coin.Outpoint, effectiveBanningTime));
			coin.BannedUntilUtc = effectiveBanningTime;
			ToFile();
		}
	}

	public bool IsBanned(OutPoint outpoint)
	{
		lock (_lock)
		{
			return GetBanningStatus(outpoint) == BanningStatus.Banned;
		}
	}

	public static CoinPrison CreateOrLoadFromFile(string containingDirectory)
	{
		string prisonFilePath = Path.Combine(containingDirectory, "PrisonedCoins.json");
		try
		{
			IoHelpers.EnsureFileExists(prisonFilePath);

			string data = File.ReadAllText(prisonFilePath);
			if (string.IsNullOrWhiteSpace(data))
			{
				Logger.LogDebug("Prisoned coins file is empty.");
				return new(prisonFilePath, []);
			}
			var prisonedCoinRecords = JsonConvert.DeserializeObject<HashSet<PrisonedCoinRecord>>(data)
				?? throw new InvalidDataException("Prisoned coins file is corrupted.");

			return new(prisonFilePath, prisonedCoinRecords.ToDictionary(x=> x.Outpoint, x=>x));
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(CoinPrison)}. Deleting corrupt file.", exc);
			File.Delete(prisonFilePath);
			return new(prisonFilePath, []);
		}
	}

	public void UpdateWallet(Wallet wallet)
	{
		lock (_lock)
		{
			foreach (var coin in wallet.Coins.Where(c => GetBanningStatus(c.Outpoint) == BanningStatus.BanningExpired))
			{
				if (bannedCoins.Remove(coin.Outpoint))
				{
					coin.BannedUntilUtc = null;
				}
			}

			ToFile();
		}
	}

	public void Dispose()
	{
		lock (_lock)
		{
			ToFile();
		}
	}

	private BanningStatus GetBanningStatus(OutPoint outpoint)
	{
		return !bannedCoins.TryGetValue(outpoint, out var bannedCoin)
			? BanningStatus.NonBanned
			: DateTimeOffset.UtcNow < bannedCoin.BannedUntil
				? BanningStatus.Banned
				: BanningStatus.BanningExpired;
	}

	private void ToFile()
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return;
		}

		IoHelpers.EnsureFileExists(filePath);
		string json = JsonConvert.SerializeObject(bannedCoins.Values, Formatting.Indented);
		File.WriteAllText(filePath, json);
	}

	private static DateTimeOffset EffectiveBanningTime(DateTimeOffset bannedUntil)
	{
		var maxBanDateTime = DateTimeOffset.UtcNow + MaxDaysToTrustLocalPrison;
		var maxBanUnixDateTime = long.Min(maxBanDateTime.ToUnixTimeSeconds(), bannedUntil.ToUnixTimeSeconds());
		return DateTimeOffset.FromUnixTimeSeconds(maxBanUnixDateTime);
	}
}
