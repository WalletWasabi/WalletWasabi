using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
	public class TumblerStatusResponse : BaseResponse
	{
		public TumblerStatusResponse() => Success = true;
		public bool IsTumblerOnline { get; set; }
		public string TumblerDenomination { get; set; }
		public int TumblerAnonymitySet { get; set; }
		public int TumblerNumberOfPeers { get; set; }
		public string TumblerFeePerRound { get; set; }
		public int TumblerWaitedInInputRegistration { get; set; }
		public string TumblerPhase { get; set; }
	}
}