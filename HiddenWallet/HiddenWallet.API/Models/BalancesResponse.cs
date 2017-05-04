using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
	public class BalancesResponse : BaseResponse
	{
		public BalancesResponse() => Success = true;
		public decimal Available { get; set; }
		public decimal Incoming { get; set; }
	}
}
