using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace WalletWasabi.Backend.Models
{
	/// <summary>
	/// Satoshi per bytes.
	/// </summary>
    public class FeeEstimationPair
	{
		[Required]
		public long Economical { get; set; }
		[Required]
		public long Conservative { get; set; }
	}
}
