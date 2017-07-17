using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
	public class TumblerServerResponse : BaseResponse
	{
		public TumblerServerResponse() => Success = true;
		public string Address { get; set; }
		public string Status { get; set; }
		public string Denomination { get; set; }
		public string FeePercent { get; set; }
		public string SatoshiFeePerBytes { get; set; }
		public string CycleLengthMinutes { get; set; }
	}
}
