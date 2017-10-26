using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Models
{
    public class PhaseChangeBroadcast
    {
		public string NewPhase { get; set; }
		public string Message { get; set; } //Optional
	}
}
