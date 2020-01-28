using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;

namespace WalletWasabi.CoinJoin.Client.Clients.Queuing
{
	public class DequeueResult
	{
		public IDictionary<DequeueReason, IEnumerable<SmartCoin>> Successful { get; }
		public IDictionary<DequeueReason, IEnumerable<SmartCoin>> Unsuccessful { get; }

		public DequeueResult(IDictionary<DequeueReason, IEnumerable<SmartCoin>> successful, IDictionary<DequeueReason, IEnumerable<SmartCoin>> unsuccessful)
		{
			Successful = Guard.NotNull(nameof(successful), successful);
			Unsuccessful = Guard.NotNull(nameof(unsuccessful), unsuccessful);
		}
	}
}
