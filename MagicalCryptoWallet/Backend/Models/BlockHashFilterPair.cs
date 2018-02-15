using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models
{
    public class BlockHashFilterPair
    {
		public uint256 BlockHash { get; set; }
		public string FilterHex { get; set; }
	}
}
