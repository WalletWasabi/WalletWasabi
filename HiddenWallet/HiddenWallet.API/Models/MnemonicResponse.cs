using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
    public class MnemonicResponse : BaseResponse
	{
		public MnemonicResponse() => Success = true;
		public string Mnemonic { get; set; }
	}
}
