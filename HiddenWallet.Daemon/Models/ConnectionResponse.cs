using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class ConnectionResponse: BaseResponse
	{
		public ConnectionResponse() => Success = true;
		public bool IsMixOngoing { get; set; }
	}
}
