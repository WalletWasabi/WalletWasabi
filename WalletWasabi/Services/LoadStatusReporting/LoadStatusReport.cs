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

		public decimal TransactionProcessProgressPercentage
			=> AllTransactionsToBeProcessed == 0 ?
				100m
				: TransactionsProcessed / AllTransactionsToBeProcessed * 100;

		public uint AllFiltersToBeProcessed { get; set; }
		public uint FiltersProcessed { get; set; }

		public decimal FilterProcessProgressPercentage
			=> AllFiltersToBeProcessed == 0 ?
				100m
				: FiltersProcessed / AllFiltersToBeProcessed * 100;
	}
}
