using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace WalletWasabi.Gui.Models.StatusBarStatuses
{
	public class StatusSet : ReactiveObject
	{
		private StatusPriority _status;

		private HashSet<StatusPriority> ActiveStatuses { get; }
		private object ActiveStatusesLock { get; }

		public StatusSet()
		{
			ActiveStatuses = new HashSet<StatusPriority>() { StatusPriority.Ready };
			ActiveStatusesLock = new object();
			CurrentStatus = StatusPriority.Loading;
		}

		public StatusPriority CurrentStatus
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		public bool TryAddStatus(StatusPriority status)
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

		public bool TryRemoveStatus(params StatusPriority[] statuses)
		{
			bool ret = false;
			lock (ActiveStatusesLock)
			{
				foreach (StatusPriority status in statuses.ToHashSet())
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
