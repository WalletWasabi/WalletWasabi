using ConcurrentCollections;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Clients
{
    public class Alice
    {
		public Guid UniqueId { get; set; }
		public ConcurrentHashSet<(TxOut Output, OutPoint OutPoint)> Inputs { get; set; }
		public BitcoinWitPubKeyAddress ChangeOutput { get; set; }
		public Money ChangeAmount { get; set; }
		public AliceState State { get; set; }
	}
}
