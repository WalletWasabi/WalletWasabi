using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
    public class BuildTransactionResponse : BaseResponse
	{
		public BuildTransactionResponse() => Success = true;
		public bool SpendsUnconfirmed { get; set; }
		public string Fee { get; set; }
		public string FeePercentOfSent { get; set; }
		public string Hex { get; set; }
		public string Transaction { get; set; }
	}
}
