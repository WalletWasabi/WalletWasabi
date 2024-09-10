using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionBroadcastEntry
{
	public TransactionBroadcastEntry(SmartTransaction transaction)
	{
		Transaction = transaction;
		TransactionId = Transaction.GetHash();
		BroadcastCompleted = new TaskCompletionSource<EndPoint[]>();
		PropagationConfirmed = new TaskCompletionSource<EndPoint[]>();
	}

	public SmartTransaction Transaction { get; }
	public uint256 TransactionId { get; }

	public TaskCompletionSource<EndPoint[]> BroadcastCompleted { get; }
	public TaskCompletionSource<EndPoint[]> PropagationConfirmed { get; }

	private readonly HashSet<EndPoint> _broadcastedTo = new();
	private readonly HashSet<EndPoint> _confirmedBy = new();
	private readonly object _syncObj = new();

	public void BroadcastedTo(EndPoint nodeEndpoint)
	{
		lock (_syncObj)
		{
			if (_broadcastedTo.Add(nodeEndpoint) && _broadcastedTo.Count > 1)
			{
				BroadcastCompleted.TrySetResult(_broadcastedTo.ToArray());
			}
		}
	}

	public bool WasBroadcastedTo(EndPoint nodeEndpoint)
	{
		return _broadcastedTo.Contains(nodeEndpoint);
	}

	public void ConfirmPropagationOnce(EndPoint nodeEndpoint)
	{
		lock (_syncObj)
		{
			if (_confirmedBy.Add(nodeEndpoint) && _confirmedBy.Count > 1)
			{
				PropagationConfirmed.TrySetResult(_confirmedBy.ToArray());
			}
		}
	}

	public void ConfirmPropagationForGood(EndPoint nodeEndpoint)
	{
		PropagationConfirmed.TrySetResult([nodeEndpoint]);
	}
}
