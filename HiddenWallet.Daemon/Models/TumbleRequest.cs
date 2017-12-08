using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class TumbleRequest
	{
		public int RoundCount { get; set; }
		public string From { get; set; }
		public string To { get; set; }
	}
}
