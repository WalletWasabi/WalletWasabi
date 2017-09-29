using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class WalletRecoverRequest
    {
		public string Password { get; set; }
		public string Mnemonic { get; set; }
		public string CreationTime { get; set; }
	}
}
