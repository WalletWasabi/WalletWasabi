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
		CoinJoinIds = new ConcurrentDictionary<uint256, byte>(coinJoinHashes.ToDictionary(x => x, x => byte.MinValue));
	}

	public InMemoryCoinJoinIdStore() : this(Enumerable.Empty<uint256>())
	{
	}

	// We would use a HashSet here but ConcurrentHashSet not exists.
	private ConcurrentDictionary<uint256, byte> CoinJoinIds { get; }

	public bool Contains(uint256 hash)
	{
		return CoinJoinIds.ContainsKey(hash);
	}

	public void Add(uint256 hash)
	{
		// The byte is just a dummy value, we are not using it.
		CoinJoinIds.TryAdd(hash, byte.MinValue);
	}

	public static InMemoryCoinJoinIdStore LoadFromFile(string filePath)
	{
		var lines = File.Exists(filePath)
			? File.ReadAllLines(filePath).Select(x => uint256.Parse(x))
			: Enumerable.Empty<uint256>();

		var store = new InMemoryCoinJoinIdStore(lines);
		return store;
	}
}
