using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

public interface ICoinJoinIdStore
{
	public bool TryAdd(uint256 id);

	public bool Contains(uint256 id);
}
