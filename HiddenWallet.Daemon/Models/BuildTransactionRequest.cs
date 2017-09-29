using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class BuildTransactionRequest
	{
		public string Password { get; set; }
		public string Address { get; set; }
		public string Amount { get; set; }
		public string FeeType { get; set; }
	}
}
