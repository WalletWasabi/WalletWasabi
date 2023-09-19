using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public class CoinJoinIdStore : InMemoryCoinJoinIdStore
{
	// Only for testing purposes.
	internal CoinJoinIdStore() : this(Enumerable.Empty<uint256>(), string.Empty)
	{
	}

	public CoinJoinIdStore(IEnumerable<uint256> coinjoinIds, string coinJoinIdStoreFilePath) : base(coinjoinIds)
	{
		CoinJoinIdStoreFilePath = coinJoinIdStoreFilePath;
	}

	private string CoinJoinIdStoreFilePath { get; set; }
	private object FileWriteLock { get; set; } = new();

	public override bool TryAdd(uint256 id)
	{
		if (base.TryAdd(id))
		{
			try
			{
				lock (FileWriteLock)
				{
					File.AppendAllLines(CoinJoinIdStoreFilePath, new[] { id.ToString() });
					return true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Could not write to file {CoinJoinIdStoreFilePath}.", ex);
			}
		}
		return false;
	}

	public override bool Contains(uint256 id)
	{
		return base.Contains(id);
	}

	public static CoinJoinIdStore Create(string coinJoinIdStoreFilePath)
	{
		bool updateFile = false;
		var coinjoins = Enumerable.Empty<string>();
		if (!File.Exists(coinJoinIdStoreFilePath))
		{
			IoHelpers.EnsureFileExists(coinJoinIdStoreFilePath);
		}
		else
		{
			coinjoins = File.ReadAllLines(coinJoinIdStoreFilePath).Where(line => !string.IsNullOrEmpty(line));
		}

		// Checking duplicates.
		var distinctCoinJoins = coinjoins.Distinct();
		if (distinctCoinJoins.Count() != coinjoins.Count())
		{
			coinjoins = distinctCoinJoins;
			updateFile = true;
		}

		// Parse and filter all invalid ids.
		var parsedCoinjoinIds = GetValidCoinjoinIds(coinjoins, out List<string> validCoinjoinIds, out bool wasError);

		if (updateFile || wasError)
		{
			File.WriteAllLines(coinJoinIdStoreFilePath, validCoinjoinIds);
		}

		Logger.LogInfo($"{parsedCoinjoinIds.Count()} coinjoins were imported from files.");

		return new CoinJoinIdStore(parsedCoinjoinIds, coinJoinIdStoreFilePath);
	}

	internal static IEnumerable<uint256> GetValidCoinjoinIds(IEnumerable<string> coinjoins, out List<string> validCoinJoins, out bool wasError)
	{
		wasError = false;
		validCoinJoins = new List<string>();
		List<uint256> parsedIds = new();
		foreach (string id in coinjoins)
		{
			if (uint256.TryParse(id, out uint256 parsedId))
			{
				parsedIds.Add(parsedId);
				validCoinJoins.Add(id);
			}
			else
			{
				wasError = true;
				Logger.LogError($"Failed to parse coinjoin id: {id}.");
			}
		}
		return parsedIds;
	}
}
