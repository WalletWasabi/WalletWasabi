using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
	public class ReceiveResponse : BaseResponse
	{
		public ReceiveResponse() => Success = true;
		public string[] Addresses { get; set; }
		public string ExtPubKey { get; set; }
	}
}
