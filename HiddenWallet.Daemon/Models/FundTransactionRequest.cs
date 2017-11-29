using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class FundTransactionRequest
	{
		public string Password { get; set; }
		public string Address { get; set; }
		public string FeeType { get; set; }
		public string[] Inputs { get; set; }
	}
}
