using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models
{
    public class BlockHashFilterPair
    {
		public string BlockHash { get; set; }
		public string FilterHex { get; set; }
	}
}
