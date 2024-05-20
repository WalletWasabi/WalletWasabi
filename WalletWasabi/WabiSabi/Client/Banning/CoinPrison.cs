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

public class CoinPrison : IDisposable
{
	// Coins with banning time longer than this will be reduced to a random value between 2 and 4 days.
	private static readonly int MaxDaysToTrustLocalPrison = 4;

	public CoinPrison(string filePath)
	{
		FilePath = filePath;
	}

	private HashSet<PrisonedCoinRecord> BannedCoins { get; set; } = new();
	private string FilePath { get; }
	private object Lock { get; set; } = new();

	public bool TryGetOrRemoveBannedCoin(SmartCoin coin, [NotNullWhen(true)] out DateTimeOffset? bannedUntil)
	{
		lock (Lock)
		{
			bannedUntil = null;
			if (BannedCoins.SingleOrDefault(record => record.Outpoint == coin.Outpoint) is { } record)
			{
				if (DateTimeOffset.UtcNow < record.BannedUntil)
				{
					bannedUntil = record.BannedUntil;
					return true;
				}
				RemoveBannedCoinNoLock(coin);
			}
			return false;
		}
	}

	public void Ban(SmartCoin coin, DateTimeOffset until)
	{
		lock (Lock)
		{
			if (BannedCoins.Any(record => record.Outpoint == coin.Outpoint))
			{
				return;
			}

			until = ReduceBanningTimeIfNeeded(until);
			BannedCoins.Add(new(coin.Outpoint, until));
			coin.BannedUntilUtc = until;
			ToFile();
		}
	}

	private void RemoveBannedCoinNoLock(SmartCoin coin)
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

	/// <summary>
	///	Reduces local banning time, which we save to disk, if it's longer than the <see cref="MaxDaysToTrustLocalPrison"/>.
	///	This is to avoid saving absurd long banning times like 1-2 years.
	///	With this, the coin will retry to participate in a CJ in every 2-4 days and see if the coin is still banned or not according to the backend.
	///	Random values are used for the new banning period so we don't leak information to the coordinator when the coins get released from the local prison.
	/// </summary>
	/// <param name="bannedUntil">Banning time according to the backend.</param>
	/// <returns>New banning period we want to save to file on client side.</returns>
	private static DateTimeOffset ReduceBanningTimeIfNeeded(DateTimeOffset bannedUntil)
	{
		var currentDate = DateTimeOffset.UtcNow;
		if (bannedUntil > currentDate.AddDays(MaxDaysToTrustLocalPrison))
		{
			Random random = new();
			int minHours = ((MaxDaysToTrustLocalPrison * 24) - 1) / 2;
			int maxHours = (MaxDaysToTrustLocalPrison * 24) - 1;
			int randomHours = random.Next(minHours, maxHours);
			int randomMinutes = random.Next(0, 60);
			int randomSeconds = random.Next(0, 60);

			return currentDate.AddHours(randomHours).AddMinutes(randomMinutes).AddSeconds(randomSeconds);
		}

		return bannedUntil;
	}

	public static CoinPrison CreateOrLoadFromFile(string containingDirectory)
	{
		string prisonFilePath = Path.Combine(containingDirectory, "PrisonedCoins.json");
		HashSet<PrisonedCoinRecord> prisonedCoinRecords = new();
		try
		{
			IoHelpers.EnsureFileExists(prisonFilePath);

			string data = File.ReadAllText(prisonFilePath);
			if (string.IsNullOrWhiteSpace(data))
			{
				Logger.LogDebug("Prisoned coins file is empty.");
				return new(prisonFilePath);
			}
			prisonedCoinRecords = JsonConvert.DeserializeObject<HashSet<PrisonedCoinRecord>>(data)
				?? throw new InvalidDataException("Prisoned coins file is corrupted.");
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(CoinPrison)}. Deleting corrupt file.", exc);
			File.Delete(prisonFilePath);
		}

		foreach (var item in prisonedCoinRecords)
		{
			item.BannedUntil = ReduceBanningTimeIfNeeded(item.BannedUntil);
		}

		return new(prisonFilePath) { BannedCoins = prisonedCoinRecords };
	}

	public void UpdateWallet(Wallet wallet)
	{
		foreach (var coin in wallet.Coins)
		{
			if (TryGetOrRemoveBannedCoin(coin, out var bannedUntil))
			{
				coin.BannedUntilUtc = bannedUntil;
			}
		}
	}

	public void Dispose()
	{
		ToFile();
	}
}
