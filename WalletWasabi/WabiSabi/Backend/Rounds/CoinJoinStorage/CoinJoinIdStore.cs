using NBitcoin;
using System.IO;
using System.Linq;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public class CoinJoinIdStore : ICoinJoinIdStore
{
	private InMemoryCoinJoinIdStore InMemoryCoinJoinIdStore { get; set; }
	private string CoinJoinIdStoreFilePath { get; set; }
	private object FileWriteLock { get; set; } = new();

	public CoinJoinIdStore() : this(string.Empty)
	{
	}

	public CoinJoinIdStore(string coinJoinIdStoreFilePath)
	{
		CoinJoinIdStoreFilePath = coinJoinIdStoreFilePath;
		InMemoryCoinJoinIdStore = InMemoryCoinJoinIdStore.LoadFromFile(coinJoinIdStoreFilePath);
	}

	public void Append(uint256 id)
	{
		if (InMemoryCoinJoinIdStore.TryAdd(id))
		{
			try
			{
				lock (FileWriteLock)
				{
					File.AppendAllLines(CoinJoinIdStoreFilePath, new[] { id.ToString() });
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Could not write to file {CoinJoinIdStoreFilePath}.", ex);
			}
		}
	}

	public bool Contains(uint256 id)
	{
		return InMemoryCoinJoinIdStore.Contains(id);
	}

	public static void ImportWW1CoinJoinsToWW2(string ww1CoinJoinsFilePath, string coinJoinIdStoreFilePath)
	{
		try
		{
			var oldCoinjoins = File.ReadAllLines(ww1CoinJoinsFilePath);

			var newCoinjoins = File.ReadAllLines(coinJoinIdStoreFilePath);
			var missingOldCoinjoins = oldCoinjoins.Except(newCoinjoins);
			if (missingOldCoinjoins.Any())
			{
				File.AppendAllLines(coinJoinIdStoreFilePath, missingOldCoinjoins);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to import WW1 coinjoins. Reason:", ex);
		}
	}
}
