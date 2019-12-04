using Avalonia.Threading;
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
		private StatusType _status;

		private HashSet<StatusType> ActiveStatuses { get; }
		private object ActiveStatusesLock { get; }

		public StatusSet()
		{
			ActiveStatuses = new HashSet<StatusType>() { StatusType.Ready };
			ActiveStatusesLock = new object();
			CurrentStatus = StatusType.Loading;
		}

		public StatusType CurrentStatus
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		public bool TryAddStatus(StatusType status)
		{
			bool ret;
			lock (ActiveStatusesLock)
			{
				ret = ActiveStatuses.Add(status);
			}

			if (ret)
			{
				AdjustCurrentStatus();
			}

			return ret;
		}

		private void AdjustCurrentStatus()
		{
			Dispatcher.UIThread.PostLogException(() => CurrentStatus = ActiveStatuses.Min());
		}

		public bool TryRemoveStatus(params StatusType[] statuses)
		{
			bool ret = false;
			lock (ActiveStatusesLock)
			{
				foreach (StatusType status in statuses.ToHashSet())
				{
					ret = ActiveStatuses.Remove(status) || ret;
				}
			}

			if (ret)
			{
				AdjustCurrentStatus();
			}

			return ret;
		}
	}
}
