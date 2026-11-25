using System.IO;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
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

	private readonly Lock _lock = new();

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

			ToFileNoLock();
		}
	}

	public bool IsBanned(OutPoint outpoint)
	{
		lock (_lock)
		{
			return GetBanningStatusNoLock(outpoint) is (BanningStatus.Banned, _);
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

			return new(prisonFilePath, prisonedCoinRecords.ToHashSet().ToDictionary(x => x.Outpoint, x => x));
		}
		catch (Exception ex)
		{
			Logger.LogError($"There was an error during loading {nameof(CoinPrison)}. Deleting corrupt file.", ex);
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
				var (banningStatus, bannedUntil) = GetBanningStatusNoLock(coin.Outpoint);
				if (banningStatus is BanningStatus.BanningExpired)
				{
					if (bannedCoins.Remove(coin.Outpoint))
					{
						coin.BannedUntilUtc = null;
						needToSave = true;
					}
				}
				else if (banningStatus is BanningStatus.Banned)
				{
					coin.BannedUntilUtc = bannedUntil;
				}
			}

			if (needToSave)
			{
				ToFileNoLock();
			}
		}
	}

	private (BanningStatus, DateTimeOffset?) GetBanningStatusNoLock(OutPoint outpoint)
	{
		var status = !bannedCoins.TryGetValue(outpoint, out var bannedCoin)
			? BanningStatus.NonBanned
			: DateTimeOffset.UtcNow < bannedCoin.BannedUntil
				? BanningStatus.Banned
				: BanningStatus.BanningExpired;
		return (status, bannedCoin?.BannedUntil);
	}

	private void ToFileNoLock()
	{
		IoHelpers.EnsureFileExists(filePath);
		var json = JsonEncoder.ToReadableString(bannedCoins.Values, Encode.ClientPrison);
		File.WriteAllText(filePath, json);
	}

	public void Dispose()
	{
		lock (_lock)
		{
			ToFileNoLock();
		}
	}
}
