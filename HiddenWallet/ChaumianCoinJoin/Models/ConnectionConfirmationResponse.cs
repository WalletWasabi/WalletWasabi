using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.ChaumianCoinJoin.Models
{
    public class ConnectionConfirmationResponse : BaseResponse
	{
		public ConnectionConfirmationResponse() => Success = true;
		public string RoundHash { get; set; }
	}
}
