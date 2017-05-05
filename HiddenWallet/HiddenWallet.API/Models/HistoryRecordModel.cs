using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.API.Models
{
    public class HistoryRecordModel
	{
		public string TxId { get; set; }
		public string Amount { get; set; }
		public bool Confirmed { get; set; }
		public string Height { get; set; }
	}
}
