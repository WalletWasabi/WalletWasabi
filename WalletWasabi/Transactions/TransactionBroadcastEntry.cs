using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace WalletWasabi.Transactions
{
	public class TransactionBroadcastEntry
	{
		public Transaction Transaction { get; }
		public uint256 TransactionId { get; }
		public string NodeRemoteSocketEndpoint { get; }

		private bool Broadcasted { get; set; }
		private int PropagationConfirmations { get; set; }

		private object Lock { get; }

		public TransactionBroadcastEntry(Transaction transaction, string nodeRemoteSocketEndpoint)
		{
			Lock = new object();
			Transaction = transaction;
			TransactionId = Transaction.GetHash();
			Broadcasted = false;
			PropagationConfirmations = 0;
			NodeRemoteSocketEndpoint = nodeRemoteSocketEndpoint;
		}

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
				PropagationConfirmations++;
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
}
