using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public class InMemoryCoinJoinIdStore
{
	public InMemoryCoinJoinIdStore(IEnumerable<uint256> coinJoinHashes)
	{
		CoinJoinIds = new ConcurrentDictionary<uint256, bool>(coinJoinHashes.ToDictionary(x => x, x => true));
	}

	public InMemoryCoinJoinIdStore() : this(Enumerable.Empty<uint256>())
	{
	}

	public ConcurrentDictionary<uint256, bool> CoinJoinIds { get; }

	public bool Contains(uint256 hash)
	{
		return CoinJoinIds.ContainsKey(hash);
	}

	public void Add(uint256 hash)
	{
		CoinJoinIds.TryAdd(hash, true);
	}

	public static InMemoryCoinJoinIdStore LoadFromFile(string filePath)
	{
		var lines = File.ReadAllLines(filePath).Select(x => uint256.Parse(x));
		var store = new InMemoryCoinJoinIdStore(lines);
		return store;
	}
}
