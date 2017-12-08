using HiddenWallet.SharedApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.Daemon.Models
{
    public class BuildTransactionResponse : BaseResponse
	{
		public BuildTransactionResponse() => Success = true;
		public bool SpendsUnconfirmed { get; set; }
		public string Fee { get; set; }
		public string FeePercentOfSent { get; set; }
		public string Hex { get; set; }
		public string Transaction { get; set; }
		public string ActiveOutputAddress { get; set; }
		public string ActiveOutputAmount { get; set; }
		public string ChangeOutputAddress { get; set; }
		public string ChangeOutputAmount { get; set; }
		public int NumberOfInputs { get; set; }
		public TransactionInputModel[] Inputs { get; set; }
	}
}
