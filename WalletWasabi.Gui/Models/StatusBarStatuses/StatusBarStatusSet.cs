using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace WalletWasabi.Gui.Models.StatusBarStatuses
{
	public class StatusBarStatusSet : ReactiveObject
	{
		private StatusBarStatus _status;

		private HashSet<StatusBarStatus> ActiveStatuses { get; }
		private object ActiveStatusesLock { get; }

		public StatusBarStatusSet()
		{
			ActiveStatuses = new HashSet<StatusBarStatus>() { StatusBarStatus.Ready };
			ActiveStatusesLock = new object();
			CurrentStatus = StatusBarStatus.Loading;
		}

		public StatusBarStatus CurrentStatus
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		public bool TryAddStatus(StatusBarStatus status)
		{
			bool ret;
			lock (ActiveStatusesLock)
			{
				ret = ActiveStatuses.Add(status);
				if (ret)
				{
					CurrentStatus = ActiveStatuses.Min();
				}
			}

			return ret;
		}

		public bool TryRemoveStatus(params StatusBarStatus[] statuses)
		{
			bool ret = false;
			lock (ActiveStatusesLock)
			{
				foreach (StatusBarStatus status in statuses.ToHashSet())
				{
					ret = ActiveStatuses.Remove(status) || ret;
				}

				if (ret)
				{
					CurrentStatus = ActiveStatuses.Min();
				}
			}

			return ret;
		}
	}
}
