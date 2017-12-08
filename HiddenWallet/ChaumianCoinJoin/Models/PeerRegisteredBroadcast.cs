using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.ChaumianCoinJoin.Models
{
    public class PeerRegisteredBroadcast
    {
		public int NewRegistration { get; set; }
		public string Message { get; set; } //Optional
	}
}
