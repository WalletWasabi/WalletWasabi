using NBitcoin;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public class CoinJoinIdStore : InMemoryCoinJoinIdStore
{
	private string CoinJoinIdStoreFilePath { get; set; }
	private object FileWriteLock { get; set; } = new();

	public CoinJoinIdStore() : this(Enumerable.Empty<uint256>(), string.Empty)
	{
	}

	public CoinJoinIdStore(IEnumerable<uint256> coinjoinIds, string coinJoinIdStoreFilePath) : base(coinjoinIds)
	{
		CoinJoinIdStoreFilePath = coinJoinIdStoreFilePath;
	}

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

	public static CoinJoinIdStore Create(string ww1CoinJoinsFilePath, string coinJoinIdStoreFilePath)
	{
		bool updateFile = false;
		var coinjoins = Enumerable.Empty<string>();
		if (!File.Exists(coinJoinIdStoreFilePath))
		{
			IoHelpers.EnsureContainingDirectoryExists(coinJoinIdStoreFilePath);
		}
		else
		{
			coinjoins = File.ReadAllLines(coinJoinIdStoreFilePath);
		}

		// Try to import ww1 coinjoins.
		try
		{
			var ww1Coinjoins = File.ReadAllLines(ww1CoinJoinsFilePath);

			var missingWw1Coinjoins = ww1Coinjoins.Except(coinjoins);

			if (missingWw1Coinjoins.Any())
			{
				coinjoins = missingWw1Coinjoins.Concat(coinjoins);
				updateFile = true;
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to import WW1 coinjoins. Reason:", ex);
		}

		// Checking duplicates.
		var distinctCoinJoins = coinjoins.Distinct();
		if (distinctCoinJoins.Count() != coinjoins.Count())
		{
			coinjoins = distinctCoinJoins;
			updateFile = true;
		}

		var parsedIds = ParseIds(coinjoins, out var stringIds);
		if (stringIds.Count() != coinjoins.Count())
		{
			updateFile = true;
		}

		if (updateFile)
		{
			File.WriteAllLines(coinJoinIdStoreFilePath, stringIds);
		}

		return new CoinJoinIdStore(parsedIds, coinJoinIdStoreFilePath);
	}

	private static IEnumerable<uint256> ParseIds(IEnumerable<string> coinjoins, out IEnumerable<string> stringIds)
	{
		stringIds = Enumerable.Empty<string>();
		var ids = Enumerable.Empty<uint256>();
		foreach (var line in coinjoins)
		{
			if (uint256.TryParse(line, out uint256 id))
			{
				ids = ids.Append(id);
				stringIds = stringIds.Append(line);
			}
			else
			{
				Logger.LogError($"Failed to parse coinjoin id: {line}.");
			}
		}
		return ids;
	}
}
