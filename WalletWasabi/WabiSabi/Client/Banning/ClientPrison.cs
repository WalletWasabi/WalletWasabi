using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class ClientPrison : PeriodicRunner
{
	public ClientPrison(string containingDirectory) : base(TimeSpan.FromSeconds(10))
	{
		FilePath = string.IsNullOrEmpty(containingDirectory) ? "" : Path.Combine(containingDirectory, "PrisonedCoins.json");

		ChangeId = Guid.NewGuid();
		LastKnownId = ChangeId;
	}

	public ClientPrison() : this("")
	{
	}

	[JsonIgnore]
	public string FilePath { get; set; }

	[JsonIgnore]
	public Guid ChangeId { get; private set; }

	public Guid LastKnownId { get; private set; }
	public List<PrisonedCoinRecord> PrisonedCoins { get; set; } = new();

	private object PrisonedCoinsLock { get; } = new object();

	public void AddCoin(SmartCoin coin, DateTimeOffset bannedUntil)
	{
		lock (PrisonedCoinsLock)
		{
			if (PrisonedCoins.Any(record => record.Outpoint == coin.Outpoint))
			{
				return;
			}
			PrisonedCoins.Add(new(coin.Outpoint, bannedUntil));
			ChangeId = Guid.NewGuid();
		}
	}

	private void ToFile()
	{
		if (string.IsNullOrWhiteSpace(FilePath))
		{
			return;
		}

		IoHelpers.EnsureFileExists(FilePath);
		var json = JsonConvert.SerializeObject(PrisonedCoins, Formatting.Indented);
		File.WriteAllText(FilePath, json);
	}

	public static ClientPrison CreateOrLoadFromFile(string containingDirectory)
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
				return new(containingDirectory);
			}
			prisonedCoinsRecord = JsonConvert.DeserializeObject<List<PrisonedCoinRecord>>(data)
				?? throw new InvalidDataException("Prisoned coins file is corrupted.");
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(ClientPrison)}. Reseting file.", exc);
			File.Delete(prisonFilePath);
		}
		return new(containingDirectory) { PrisonedCoins = prisonedCoinsRecord };
	}

	protected override Task ActionAsync(CancellationToken cancel)
	{
		lock (PrisonedCoinsLock)
		{
			bool shouldWriteToFile = LastKnownId != ChangeId;

			if (PrisonedCoins.Any(record => DateTime.UtcNow > record.BannedUntil))
			{
				PrisonedCoins = PrisonedCoins.Where(record => DateTime.UtcNow < record.BannedUntil).ToList();
				shouldWriteToFile = true;
			}

			if (shouldWriteToFile)
			{
				LastKnownId = ChangeId;
				ToFile();
			}
		}
		return Task.CompletedTask;
	}

	public void FindAndPrisonBannedCoins(IWallet iwallet)
	{
		Wallet? wallet = iwallet as Wallet;

		var coinsToPrison = wallet?.Coins.Where(coin => coin.IsBanned) ?? new List<SmartCoin>();
		foreach (var coin in coinsToPrison)
		{
			DateTimeOffset bannedUntil = coin.BannedUntilUtc ?? throw new InvalidOperationException($"Coin was banned but {nameof(coin.BannedUntilUtc)} was null. This is impossible.");
			AddCoin(coin, bannedUntil);
		}
	}
}
