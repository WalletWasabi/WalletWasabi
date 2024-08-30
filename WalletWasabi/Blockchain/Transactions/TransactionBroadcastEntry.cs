using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionBroadcastEntry
{
	public TransactionBroadcastEntry(SmartTransaction transaction, string nodeRemoteSocketEndpoint)
	{
		Transaction = transaction;
		TransactionId = Transaction.GetHash();
		NodeRemoteSocketEndpoint = nodeRemoteSocketEndpoint;
		BroadcastCompleted = new TaskCompletionSource();
		PropagationConfirmed = new TaskCompletionSource();
	}

	public SmartTransaction Transaction { get; }
	public uint256 TransactionId { get; }
	public string NodeRemoteSocketEndpoint { get; }

	public TaskCompletionSource BroadcastCompleted { get; }
	public TaskCompletionSource PropagationConfirmed { get; }

	public void MakeBroadcasted()
	{
		BroadcastCompleted.TrySetResult();
	}

	public void ConfirmPropagationOnce()
	{
		PropagationConfirmed.TrySetResult();
	}

	public void ConfirmPropagationForGood()
	{
		PropagationConfirmed.TrySetResult();
	}
}
