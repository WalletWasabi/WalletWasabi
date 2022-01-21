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
	}

	public SmartTransaction Transaction { get; }
	public uint256 TransactionId { get; }
	public string NodeRemoteSocketEndpoint { get; }

	private bool Broadcasted { get; set; }
	private int PropagationConfirmations { get; set; }

	private object Lock { get; }

	public void MakeBroadcasted()
	{
		lock (Lock)
		{
			Broadcasted = true;
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
