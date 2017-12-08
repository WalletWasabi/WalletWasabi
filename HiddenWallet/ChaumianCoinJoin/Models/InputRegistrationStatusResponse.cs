using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianCoinJoin.Models
{
    public class InputRegistrationStatusResponse : BaseResponse
	{
		public InputRegistrationStatusResponse() => Success = true;
		public int RegisteredPeerCount { get; set; }
		public int RequiredPeerCount { get; set; }
		public int ElapsedSeconds { get; set; }
	}
}
