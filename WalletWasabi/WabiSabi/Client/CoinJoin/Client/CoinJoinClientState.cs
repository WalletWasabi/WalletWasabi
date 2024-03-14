namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public enum CoinJoinClientState
{
	Idle,

	/// <summary>Coinjoin is scheduled to happen via the auto start feature.</summary>
	InSchedule,

	InProgress,
	InCriticalPhase
}
