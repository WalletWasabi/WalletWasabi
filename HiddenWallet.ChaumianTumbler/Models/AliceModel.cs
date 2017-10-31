using ConcurrentCollections;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Models
{
    public class AliceModel
    {
		public Guid UniqueId { get; set; }
		public ConcurrentHashSet<TxOut> Inputs { get; set; }
		public BitcoinWitPubKeyAddress ChangeOutput { get; set; }
	}
}
