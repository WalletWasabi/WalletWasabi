using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class TumbleResponse : BaseResponse
	{
		public TumbleResponse() => Success = true;
		public string Transactions { get; set; }
	}
}
