using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class CoinPrison
{
	public CoinPrison(string filePath)
	{
		FilePath = filePath;
	}

	public List<PrisonedCoinRecord> BannedCoins { get; set; } = new();
	public string FilePath { get; set; }
	private object Lock { get; set; } = new();

	public bool TryGetOrRemoveBannedCoin(SmartCoin coin, DateTimeOffset when, [NotNullWhen(true)] out DateTimeOffset? bannedUntil)
	{
		bannedUntil = null;
		if (BannedCoins.SingleOrDefault(record => record.Outpoint == coin.Outpoint) is { } record)
		{
			if (when < record.BannedUntil)
			{
				bannedUntil = record.BannedUntil;
				return true;
			}
			RemoveBannedCoin(coin);
		}
		return false;
	}

	public void Add(SmartCoin coin, DateTimeOffset until)
	{
		if (BannedCoins.SingleOrDefault(record => record.Outpoint == coin.Outpoint) is { } record)
		{
			if (record.BannedUntil >= until)
			{
				return;
			}
		}
		BannedCoins.Add(new(coin.Outpoint, until));
		ToFile();
	}

	public void RemoveBannedCoin(SmartCoin coin)
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
		lock (Lock)
		{
		if (string.IsNullOrWhiteSpace(FilePath))
		{
			return;
		}

		IoHelpers.EnsureFileExists(FilePath);
		string json = JsonConvert.SerializeObject(BannedCoins, Formatting.Indented);
		File.WriteAllText(FilePath, json);
	}
	}

	public static CoinPrison CreateOrLoadFromFile(string containingDirectory)
	{
		string prisonFilePath = Path.Combine(containingDirectory, "PrisonedCoins.json");
		List<PrisonedCoinRecord> prisonedCoinsRecord = new();
		try
		{
			IoHelpers.EnsureFileExists(prisonFilePath);

			string data = File.ReadAllText(prisonFilePath);
			if (string.IsNullOrWhiteSpace(data))
			{
				Logger.LogDebug("Prisoned coins file is empty.");
				return new(prisonFilePath);
			}
			prisonedCoinsRecord = JsonConvert.DeserializeObject<List<PrisonedCoinRecord>>(data)
				?? throw new InvalidDataException("Prisoned coins file is corrupted.");
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(CoinPrison)}. Reseting file.", exc);
			File.Delete(prisonFilePath);
		}
		return new(prisonFilePath) { BannedCoins = prisonedCoinsRecord };
	}
}
