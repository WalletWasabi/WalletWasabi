using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class PrisonClient
{
	public PrisonClient(string dataDir)
	{
		FilePath = Path.Combine(dataDir, "PrisonClient.json");
	}

	[JsonIgnore]
	public string FilePath { get; set; }

	[JsonProperty(ItemConverterType = typeof(OutPointWithDateConverter), PropertyName = "BannedCoinsFromCoinJoin")]
	public Dictionary<OutPoint, DateTimeOffset> BannedCoins { get; set; } = new();

	public bool TryAddCoin(SmartCoin coin, DateTimeOffset bannedUntil)
	{
		BannedCoins.Add(coin.Outpoint, bannedUntil);
		ToFile();
		return true;
	}

	public void ToFile()
	{
		var json = JsonConvert.SerializeObject(this);
		File.WriteAllText(FilePath, json);
	}

	public static PrisonClient CreateOrLoadFromFile(string dataDir)
	{
		string prisonFilePath = Path.Combine(dataDir, "PrisonClient.json");
		IoHelpers.EnsureFileExists(prisonFilePath);
		var data = File.ReadAllText(prisonFilePath);
		var prisonClient = JsonConvert.DeserializeObject<PrisonClient>(data);
		return prisonClient;
	}
}
