using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionBroadcastEntry
{
	public TransactionBroadcastEntry(SmartTransaction transaction, string nodeRemoteSocketEndpoint)
	{
		Lock = new object();
		Transaction = transaction;
		TransactionId = Transaction.GetHash();
		Broadcasted = false;
		PropagationConfirmations = 0;
		NodeRemoteSocketEndpoint = nodeRemoteSocketEndpoint;
		BroadcastCompleted = new TaskCompletionSource();
		PropagationConfirmed = new TaskCompletionSource();
	}

	public SmartTransaction Transaction { get; }
	public uint256 TransactionId { get; }
	public string NodeRemoteSocketEndpoint { get; }

	public TaskCompletionSource BroadcastCompleted { get; }
	public TaskCompletionSource PropagationConfirmed { get; }
	private bool Broadcasted { get; set; }
	private int PropagationConfirmations { get; set; }

	private object Lock { get; }

	public void MakeBroadcasted()
	{
		lock (Lock)
		{
			Broadcasted = true;
			BroadcastCompleted.TrySetResult();
		}
	}

	public bool IsBroadcasted()
	{
		lock (Lock)
		{
			return Broadcasted;
		}
	}

	public void ConfirmPropagationOnce()
	{
		lock (Lock)
		{
			Broadcasted = true;
			PropagationConfirmations++;
			if (PropagationConfirmations == 2)
			{
				PropagationConfirmed.SetResult();
			}
		}
	}

	public void ConfirmPropagationForGood()
	{
		lock (Lock)
		{
			Broadcasted = true;
			PropagationConfirmations = 21;
		}
	}

	public int GetPropagationConfirmations()
	{
		lock (Lock)
		{
			return PropagationConfirmations;
		}
	}
}
