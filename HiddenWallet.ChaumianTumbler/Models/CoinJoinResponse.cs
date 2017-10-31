using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Models
{
    public class CoinJoinResponse : BaseResponse
	{
		public CoinJoinResponse() => Success = true;
		public string Transaction { get; set; }
	}
}
