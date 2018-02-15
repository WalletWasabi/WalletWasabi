using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Backend.Models
{
	/// <summary>
	/// satoshi per bytes
	/// </summary>
    public class FeeEstimationPair
	{
		public int Economical { get; set; }
		public int Conservative { get; set; }
	}
}
