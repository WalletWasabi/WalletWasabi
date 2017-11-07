using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianCoinJoin.Models
{
    public class InputsRequest
    {
		public InputProofModel[] Inputs { get; set; }
		public string BlindedOutput { get; set; }
		public string ChangeOutput { get; set; }
	}
}
