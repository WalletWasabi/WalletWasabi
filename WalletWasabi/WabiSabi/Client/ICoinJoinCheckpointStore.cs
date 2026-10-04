using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Client;

// Optional durability boundary for hosts whose process can be terminated at any
// instant. Exceptions propagate: a failed checkpoint must prevent signing.
public interface ICoinJoinCheckpointStore
{
	bool IsReserved(OutPoint input);
	void BeginRound(uint256 roundId, IEnumerable<OutPoint> inputs);
	void BeforeSigning(uint256 roundId, uint256 transactionId);
	void EndRound(uint256 roundId, EndRoundState outcome);
}
