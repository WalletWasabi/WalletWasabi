using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class CoinPrison
{
	public CoinPrison(string filePath)
	{
		FilePath = filePath;
	}

	private HashSet<PrisonedCoinRecord> BannedCoins { get; set; } = new();
	public string FilePath { get; set; }
	private object Lock { get; set; } = new();

	public bool TryGetOrRemoveBannedCoin(SmartCoin coin, DateTimeOffset banDeadlineTime, [NotNullWhen(true)] out DateTimeOffset? bannedUntil)
	{
		lock (Lock)
		{
			bannedUntil = null;
			if (BannedCoins.SingleOrDefault(record => record.Outpoint == coin.Outpoint) is { } record)
			{
				if (banDeadlineTime < record.BannedUntil)
				{
					bannedUntil = record.BannedUntil;
					return true;
				}
				RemoveBannedCoin(coin);
			}
			return false;
		}
	}

	public void Ban(SmartCoin coin, DateTimeOffset until)
	{
		lock (Lock)
		{
			if (BannedCoins.SingleOrDefault(record => record.Outpoint == coin.Outpoint) is { } record)
			{
				return;
			}
			BannedCoins.Add(new(coin.Outpoint, until));
			coin.BannedUntilUtc = until;
			ToFile();
		}
	}

	private void RemoveBannedCoin(SmartCoin coin)
	{
		var recordToRemove = BannedCoins.SingleOrDefault(record => coin.Outpoint == record.Outpoint);
		if (recordToRemove == null)
		{
			Logger.LogError($"Tried to remove {nameof(coin)} from {nameof(BannedCoins)}, but {nameof(coin)} was null.");
			return;
		}
		BannedCoins.Remove(recordToRemove);
		coin.BannedUntilUtc = null;
		ToFile();
	}

	private void ToFile()
	{
		if (string.IsNullOrWhiteSpace(FilePath))
		{
			return;
		}

		IoHelpers.EnsureFileExists(FilePath);
		string json = JsonConvert.SerializeObject(BannedCoins, Formatting.Indented);
		File.WriteAllText(FilePath, json);
	}

	public static CoinPrison CreateOrLoadFromFile(string containingDirectory)
	{
		string prisonFilePath = Path.Combine(containingDirectory, "PrisonedCoins.json");
		HashSet<PrisonedCoinRecord> prisonedCoinsRecord = new();
		try
		{
			IoHelpers.EnsureFileExists(prisonFilePath);

			string data = File.ReadAllText(prisonFilePath);
			if (string.IsNullOrWhiteSpace(data))
			{
				Logger.LogDebug("Prisoned coins file is empty.");
				return new(prisonFilePath);
			}
			prisonedCoinsRecord = JsonConvert.DeserializeObject<HashSet<PrisonedCoinRecord>>(data)
				?? throw new InvalidDataException("Prisoned coins file is corrupted.");
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(CoinPrison)}. Deleting corrupt file.", exc);
			File.Delete(prisonFilePath);
		}
		return new(prisonFilePath) { BannedCoins = prisonedCoinsRecord };
	}

	public void UpdateWallet(Wallet wallet)
	{
		foreach (var coin in wallet.Coins)
		{
			if (TryGetOrRemoveBannedCoin(coin, DateTimeOffset.UtcNow, out var bannedUntil))
			{
				coin.BannedUntilUtc = bannedUntil;
			}
		}
	}
}
