using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Services.LoadStatusReporting
{
	public class LoadStatusReport
	{
		public LoadStatusReport(LoadStatus status)
		{
			Status = status;
		}

		public LoadStatus Status { get; }

		public uint AllTransactionsToBeProcessed { get; set; }
		public uint TransactionsProcessed { get; set; }

		public int TransactionProcessProgressPercentage
			=> AllTransactionsToBeProcessed == 0 ?
				100
				: (int)((decimal)TransactionsProcessed / AllTransactionsToBeProcessed * 100);

		public uint AllFiltersToBeProcessed { get; set; }
		public uint FiltersProcessed { get; set; }

		public int FilterProcessProgressPercentage
			=> AllFiltersToBeProcessed == 0 ?
				100
				: (int)((decimal)FiltersProcessed / AllFiltersToBeProcessed * 100);
	}
}
