using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianCoinJoin.Models
{
    public class OutputRequest
	{
		public string Output { get; set; }
		public string Signature { get; set; }
	}
}
