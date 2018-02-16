using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models
{
    public class BlockHashFilterPair
	{
		[Required]
		public string BlockHash { get; set; }
		[Required]
		public string FilterHex { get; set; }
	}
}
