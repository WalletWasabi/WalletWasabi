namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinClientStateProvider
{
	private CoinJoinClientState _highestCoinJoinState;
	private object HighestCoinJoinStateLock { get; } = new object();

	public CoinJoinClientState HighestCoinJoinState
	{
		get
		{
			lock (HighestCoinJoinStateLock)
			{
				return _highestCoinJoinState;
			}
		}
		set
		{
			lock (HighestCoinJoinStateLock)
			{
				_highestCoinJoinState = value;
			}
		}
	}
}
