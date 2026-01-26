using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBroadcasting;

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

	private readonly List<EndPoint> _broadcastTo = new();
	private readonly List<EndPoint> _confirmedBy = new();
	private readonly object _syncObj = new();

	public void BroadcastedTo(EndPoint nodeEndpoint)
	{
		lock (_syncObj)
		{
			_broadcastTo.Add(nodeEndpoint);
			var count = _broadcastTo.GroupBy(x => x).Count();
			if (count >= NetworkBroadcaster.MinBroadcastNodes)
			{
				BroadcastCompleted.TrySetResult(_broadcastTo.ToArray());
			}
		}
	}

	public void ConfirmPropagationOnce(EndPoint nodeEndpoint)
	{
		lock (_syncObj)
		{
			_confirmedBy.Add(nodeEndpoint);
			var count = _confirmedBy.GroupBy(x => x).Count();
			if (count > 1)
			{
				PropagationConfirmed.TrySetResult(_confirmedBy.ToArray());
			}
		}
	}

	public bool Is(uint256 id) =>
		id == Transaction.Transaction.GetHash() ||
		id == Transaction.Transaction.GetWitHash();
}
