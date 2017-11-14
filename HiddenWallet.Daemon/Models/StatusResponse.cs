using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
	public class StatusResponse : BaseResponse
	{
		public StatusResponse() => Success = true;
		public string WalletState { get; set; }
		public int HeaderHeight { get; set; }
		public int TrackingHeight { get; set; }
		public int ConnectedNodeCount { get; set; }
		public int MemPoolTransactionCount { get; set; }
		public string TorState { get; set; }
		public bool IsTumblerOnline { get; set; }
		public int ChangeBump { get; set; }
	}
}
