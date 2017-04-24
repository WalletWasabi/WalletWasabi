using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
	public class StatusResponse : BaseResponse
	{
		public StatusResponse() => Success = true;
		public string WalletState { get; set; }
		public int HeaderHeight { get; set; }
		public int TrackingHeight { get; set; }
	}
}
