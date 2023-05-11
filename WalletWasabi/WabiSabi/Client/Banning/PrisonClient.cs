using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class PrisonClient
{
	public PrisonClient(string containingDirectory)
	{
		FilePath = Path.Combine(containingDirectory, "PrisonedCoins.json");
	}

	[JsonIgnore]
	public string FilePath { get; set; }

	public List<PrisonedCoinRecord> PrisonedCoins { get; set; } = new();

	public bool TryAddCoin(SmartCoin coin, DateTimeOffset bannedUntil)
	{
		PrisonedCoins.Add(new(coin.Outpoint, bannedUntil));
		ToFile();
		return true;
	}

	public void ToFile()
	{
		IoHelpers.EnsureFileExists(FilePath);

		var json = JsonConvert.SerializeObject(PrisonedCoins, Formatting.Indented);
		File.WriteAllText(FilePath, json);
	}

	public static PrisonClient CreateOrLoadFromFile(string containingDirectory)
	{
		string prisonFilePath = Path.Combine(containingDirectory, "PrisonedCoins.json");
		List<PrisonedCoinRecord> prisonedCoins = new();
		try
		{
			IoHelpers.EnsureFileExists(prisonFilePath);

			string data = File.ReadAllText(prisonFilePath);
			prisonedCoins = JsonConvert.DeserializeObject<List<PrisonedCoinRecord>>(data)
				?? throw new InvalidDataException("Prisoned coins file is corrupted.");
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(PrisonClient)}. Reseting file.", exc);
			File.Delete(prisonFilePath);
		}
		return new(containingDirectory) { PrisonedCoins = prisonedCoins };
	}
}
