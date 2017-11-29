using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class TransactionInputModel
	{
		public string Address { get; set; }
		public string Amount { get; set; }
		public string Hash { get; set; }
		public int Index { get; set; }
	}
}
