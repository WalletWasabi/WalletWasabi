using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using WalletWasabi.Services.LoadStatusReporting;

namespace WalletWasabi.Gui.Models
{
	public class StatusBarStatusSet : ReactiveObject
	{
		private StatusBarStatus _status;

		private Dictionary<StatusBarStatusType, StatusBarStatus> ActiveStatuses { get; }
		private object ActiveStatusesLock { get; }

		public StatusBarStatusSet()
		{
			ActiveStatuses = new Dictionary<StatusBarStatusType, StatusBarStatus>
			{
				{ StatusBarStatusType.Ready, new StatusBarStatus(StatusBarStatusType.Ready) }
			};
			ActiveStatusesLock = new object();
			CurrentStatus = new StatusBarStatus(StatusBarStatusType.Loading);
		}

		public StatusBarStatus CurrentStatus
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		public bool TrySetLoadStatus(LoadStatusReport loadStatus)
		{
			bool ret;

			if (loadStatus.Status == LoadStatus.Completed)
			{
				ret = TryRemoveStatus(((LoadStatus[])Enum.GetValues(typeof(LoadStatus))).Select(x => StatusBarStatusTypeExtensions.FromLoadStatus(x)).ToArray());
			}
			else
			{
				StatusBarStatusType statusType = StatusBarStatusTypeExtensions.FromLoadStatus(loadStatus.Status);
				StatusBarStatus status = new StatusBarStatus(statusType);
				if (loadStatus.Status == LoadStatus.ProcessingTransactions)
				{
					status = new StatusBarStatus(statusType, (int)loadStatus.TransactionProcessProgressPercentage);
				}
				else if (loadStatus.Status == LoadStatus.ProcessingFilters)
				{
					status = new StatusBarStatus(statusType, (int)loadStatus.FilterProcessProgressPercentage);
				}

				lock (ActiveStatusesLock)
				{
					ret = ActiveStatuses.TryAdd(status.Type, status);
					if (!ret && ActiveStatuses[status.Type] != status)
					{
						ActiveStatuses[status.Type] = status;
						ret = true;
					}

					if (ret)
					{
						CurrentStatus = ActiveStatuses.OrderBy(x => x.Key).First().Value;
					}
				}
			}

			return ret;
		}

		public bool TryAddStatus(StatusBarStatusType status)
		{
			bool ret;
			lock (ActiveStatusesLock)
			{
				ret = ActiveStatuses.TryAdd(status, new StatusBarStatus(status));
				if (ret)
				{
					CurrentStatus = ActiveStatuses.OrderBy(x => x.Key).First().Value;
				}
			}

			return ret;
		}

		public bool TryRemoveStatus(params StatusBarStatusType[] statuses)
		{
			bool ret = false;
			lock (ActiveStatusesLock)
			{
				foreach (var status in statuses.ToHashSet())
				{
					ret = ActiveStatuses.Remove(status, out _) || ret;
				}

				if (ret)
				{
					CurrentStatus = ActiveStatuses.OrderBy(x => x.Key).First().Value;
				}
			}

			return ret;
		}
	}
}
