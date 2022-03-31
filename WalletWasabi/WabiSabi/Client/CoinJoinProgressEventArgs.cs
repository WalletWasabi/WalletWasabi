namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinProgressEventArgs : EventArgs
{
}

public class CoinJoinEnteringInCriticalPhaseEventArgs : CoinJoinProgressEventArgs
{
}
public class CoinJoinLeavingCriticalPhaseEventArgs : CoinJoinProgressEventArgs
{
}