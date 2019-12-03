using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Services.LoadStatusReporting
{
	public enum LoadStatus
	{
		Starting,
		WaitingForBitcoinStore,
		ProcessingTransactions,
		ProcessingFilters,
		ProcessingMempool,
		Completed
	}
}
