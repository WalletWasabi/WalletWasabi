using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.ChaumianCoinJoin
{
    public class Alice
    {
		public Guid UniqueId { get; }

		public Dictionary<OutPoint, TxOut> Inputs { get; }

		public Alice(Dictionary<OutPoint, TxOut> inputs)
		{
			Guard.NotNullOrEmpty(nameof(inputs), inputs);
			Inputs = inputs;

			UniqueId = Guid.NewGuid();	
		}
    }
}
