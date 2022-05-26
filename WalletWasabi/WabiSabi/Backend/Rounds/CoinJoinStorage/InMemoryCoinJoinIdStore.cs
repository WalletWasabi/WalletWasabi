using NBitcoin;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public class InMemoryCoinJoinIdStore : ICoinJoinIdStore
{
	public InMemoryCoinJoinIdStore(IEnumerable<uint256> coinJoinHashes)
	{
		CoinJoinIds = new ConcurrentDictionary<uint256, byte>(coinJoinHashes.Distinct().ToDictionary(x => x, x => byte.MinValue));
	}

	public InMemoryCoinJoinIdStore() : this(Enumerable.Empty<uint256>())
	{
	}

	// For testing only.
	internal IEnumerable<uint256> GetCoinJoinIds => CoinJoinIds.Keys;

	// We would use a HashSet here but ConcurrentHashSet not exists.
	private ConcurrentDictionary<uint256, byte> CoinJoinIds { get; }

	public virtual bool Contains(uint256 hash)
	{
		return CoinJoinIds.ContainsKey(hash);
	}

	public virtual bool TryAdd(uint256 hash)
	{
		// The byte is just a dummy value, we are not using it.
		return CoinJoinIds.TryAdd(hash, byte.MinValue);
	}
}
