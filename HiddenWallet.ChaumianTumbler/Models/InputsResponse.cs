using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Models
{
    public class InputsResponse : BaseResponse
	{
		public InputsResponse() => Success = true;
		public string SignedBlindedOutput { get; set; }
		public string UniqueId { get; set; }
	}
}
