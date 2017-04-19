using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
    public class WalletCreateResponse : BaseResponse
	{
		public WalletCreateResponse() => Success = true;
		public string Mnemonic { get; set; }
		public string CreationTime { get; set; }
	}
}
