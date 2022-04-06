using NBitcoin;
using System.IO;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public class CoinJoinIdStore : ICoinJoinIdStore
{
	public InMemoryCoinJoinIdStore InMemoryCoinJoinIdStore { get; set; }
	public string CoinJoinsFilePath { get; set; }
	private object FileWriteLock { get; set; } = new();

	public CoinJoinIdStore() : this(string.Empty)
	{
	}

	public CoinJoinIdStore(string ww2CoinjoinsFilePath)
	{
		CoinJoinsFilePath = ww2CoinjoinsFilePath;
		InMemoryCoinJoinIdStore = InMemoryCoinJoinIdStore.LoadFromFile(ww2CoinjoinsFilePath);
	}

	public void Append(uint256 id)
	{
		InMemoryCoinJoinIdStore.Add(id);
		try
		{
			lock (FileWriteLock)
			{
				File.AppendAllLines(CoinJoinsFilePath, new[] { id.ToString() });
			}
		}
		catch (Exception ex)
		{
			Logger.LogError($"Could not write file {CoinJoinsFilePath}.", ex);
		}
	}

	public bool Contains(uint256 id)
	{
		return InMemoryCoinJoinIdStore.Contains(id);
	}
}
