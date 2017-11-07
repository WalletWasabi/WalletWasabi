using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianCoinJoin.Models
{
    public class SignatureRequest
	{
		public string UniqueId { get; set; }
		public IEnumerable<(string Witness, int Index)> Signatures { get; set; }
	}
}
