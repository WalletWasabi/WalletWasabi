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

namespace WalletWasabi.WabiSabi.Client.Banning;

public class PrisonClient : PeriodicRunner
{
	public PrisonClient(string containingDirectory) : base(TimeSpan.FromSeconds(10))
	{
		FilePath = string.IsNullOrEmpty(containingDirectory) ? "" : Path.Combine(containingDirectory, "PrisonedCoins.json");

		ChangeId = Guid.NewGuid();
		LastKnownId = ChangeId;
	}

	public PrisonClient() : this("")
	{
	}

	[JsonIgnore]
	public string FilePath { get; set; }

	[JsonIgnore]
	public Guid ChangeId { get; private set; }

	public Guid LastKnownId { get; private set; }
	public List<PrisonedCoinRecord> PrisonedCoins { get; set; } = new();

	private object Lock { get; } = new object();

	// Remove Try and bool
	public void AddCoin(SmartCoin coin, DateTimeOffset bannedUntil)
	{
		lock (Lock)
		{
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

	public static PrisonClient CreateOrLoadFromFile(string containingDirectory)
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
			Logger.LogError($"There was an error during loading {nameof(PrisonClient)}. Reseting file.", exc);
			File.Delete(prisonFilePath);
		}
		return new(containingDirectory) { PrisonedCoins = prisonedCoinsRecord };
	}

	protected override Task ActionAsync(CancellationToken cancel)
	{
		lock (Lock)
		{
			bool shouldWriteToFile = false;
			if (LastKnownId != ChangeId)
			{
				shouldWriteToFile = true;
			}

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
}
