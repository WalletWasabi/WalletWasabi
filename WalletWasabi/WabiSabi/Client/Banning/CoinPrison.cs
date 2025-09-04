using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class CoinPrison(string? filePath, Dictionary<OutPoint, PrisonedCoinRecord> bannedCoins) : IDisposable
{
	enum BanningStatus
	{
		Banned,
		BanningExpired,
		NonBanned
	}

	private readonly object _lock = new();

	public void Ban(SmartCoin coin, DateTimeOffset until)
	{
		lock (_lock)
		{
			if (bannedCoins.ContainsKey(coin.Outpoint) || until < DateTimeOffset.UtcNow)
			{
				return;
			}

			bannedCoins.Add(coin.Outpoint, new PrisonedCoinRecord(coin.Outpoint, until));
			coin.BannedUntilUtc = until;
			ToFile();
		}
	}

	public bool IsBanned(OutPoint outpoint)
	{
		lock (_lock)
		{
			return GetBanningStatus(outpoint) is (BanningStatus.Banned, _);
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
			var prisonedCoinRecords = JsonDecoder.FromString(data, Decode.Array(Decode.PrisonedCoinRecord))
				?? throw new InvalidDataException("Prisoned coins file is corrupted.");

			return new(prisonFilePath, prisonedCoinRecords.ToHashSet().ToDictionary(x=> x.Outpoint, x=>x));
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
			var needToSave = false;
			foreach (var coin in wallet.Coins)
			{
				var (banningStatus, bannedUntil) = GetBanningStatus(coin.Outpoint);
				if (banningStatus is BanningStatus.BanningExpired)
				{
					if (bannedCoins.Remove(coin.Outpoint))
					{
						coin.BannedUntilUtc = null;
						needToSave = true;
					}
				}
				else if(banningStatus is BanningStatus.Banned)
				{
					coin.BannedUntilUtc = bannedUntil;
				}
			}

			if (needToSave)
			{
				ToFile();
			}
		}
	}

	public void Dispose()
	{
		lock (_lock)
		{
			ToFile();
		}
	}

	private (BanningStatus, DateTimeOffset?) GetBanningStatus(OutPoint outpoint)
	{
		var status = !bannedCoins.TryGetValue(outpoint, out var bannedCoin)
			? BanningStatus.NonBanned
			: DateTimeOffset.UtcNow < bannedCoin.BannedUntil
				? BanningStatus.Banned
				: BanningStatus.BanningExpired;
		return (status, bannedCoin?.BannedUntil);
	}

	private void ToFile()
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return;
		}

		IoHelpers.EnsureFileExists(filePath);
		string json = JsonEncoder.ToReadableString(bannedCoins.Values, Encode.ClientPrison);
		File.WriteAllText(filePath, json);
	}
}
