using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace WalletWasabi.Backend.Models.Requests
{
    public class BroadcastRequest
	{
		[Required]
		public string Hex { get; set; }
    }
}
