using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class CoinPrison
{
	public CoinPrison() : this("")
	{
	}

	public CoinPrison(string filePath)
	{
		FilePath = filePath;
	}

	public List<PrisonedCoinRecord> BannedCoins { get; set; } = new();
	public string FilePath { get; set; }

	public bool IsCoinBanned(SmartCoin coin, DateTimeOffset when)
	{
		if (BannedCoins.SingleOrDefault(record => record.Outpoint == coin.Outpoint) is { } record)
		{
			return when > record.BannedUntil;
		}
		return false;
	}

	public void Add(SmartCoin coin, DateTimeOffset until)
	{
		if (BannedCoins.Any(record => record.Outpoint == coin.Outpoint))
		{
			return;
		}
		BannedCoins.Add(new(coin.Outpoint, until));
		ToFile();
	}

	private void ToFile()
	{
		if (string.IsNullOrWhiteSpace(FilePath))
		{
			return;
		}

		IoHelpers.EnsureFileExists(FilePath);
		string json = JsonConvert.SerializeObject(BannedCoins);
		File.WriteAllText(FilePath, json);
	}
}
