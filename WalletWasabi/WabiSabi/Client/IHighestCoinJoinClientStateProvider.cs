namespace WalletWasabi.WabiSabi.Client;

public interface IHighestCoinJoinClientStateProvider
{
	CoinJoinClientState HighestCoinJoinClientState { get; }
}
