using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Services.LoadStatusReporting;

namespace WalletWasabi.Gui.Models
{
	public static class StatusBarStatusTypeExtensions
	{
		public static LoadStatus? ToLoadStatus(this StatusBarStatusType me)
		{
			if (me == StatusBarStatusType.WalletServiceLoadingCompleted)
			{
				return LoadStatus.Completed;
			}
			if (me == StatusBarStatusType.WalletServiceLoadingProcessingFilters)
			{
				return LoadStatus.ProcessingFilters;
			}
			if (me == StatusBarStatusType.WalletServiceLoadingProcessingMempool)
			{
				return LoadStatus.ProcessingMempool;
			}
			if (me == StatusBarStatusType.WalletServiceLoadingProcessingTransactions)
			{
				return LoadStatus.ProcessingTransactions;
			}
			if (me == StatusBarStatusType.WalletServiceLoadingStarting)
			{
				return LoadStatus.Starting;
			}
			if (me == StatusBarStatusType.WalletServiceLoadingWaitingForBitcoinStore)
			{
				return LoadStatus.WaitingForBitcoinStore;
			}
			return null;
		}

		public static StatusBarStatusType FromLoadStatus(LoadStatus status)
		{
			return status switch
			{
				LoadStatus.Completed => StatusBarStatusType.WalletServiceLoadingCompleted,
				LoadStatus.ProcessingFilters => StatusBarStatusType.WalletServiceLoadingProcessingFilters,
				LoadStatus.ProcessingMempool => StatusBarStatusType.WalletServiceLoadingProcessingMempool,
				LoadStatus.ProcessingTransactions => StatusBarStatusType.WalletServiceLoadingProcessingTransactions,
				LoadStatus.Starting => StatusBarStatusType.WalletServiceLoadingStarting,
				LoadStatus.WaitingForBitcoinStore => StatusBarStatusType.WalletServiceLoadingWaitingForBitcoinStore,
				_ => throw new NotSupportedException(),
			};
		}
	}
}
