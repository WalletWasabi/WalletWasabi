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
		private Status _status;

		private HashSet<Status> ActiveStatuses { get; }
		private object ActiveStatusesLock { get; }

		public StatusSet()
		{
			ActiveStatuses = new HashSet<Status>() { Status.Set(StatusType.Ready) };
			ActiveStatusesLock = new object();
			CurrentStatus = Status.Set(StatusType.Loading);
		}

		public Status CurrentStatus
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		public void Complete(params StatusType[] statuses)
			=> Complete(statuses as IEnumerable<StatusType>);

		public void Complete(IEnumerable<StatusType> statuses)
			=> Set(statuses.Select(x => Status.Completed(x)));

		public void Set(params StatusType[] statuses)
			=> Set(statuses as IEnumerable<StatusType>);

		public void Set(IEnumerable<StatusType> statuses)
			=> Set(statuses.Select(x => Status.Set(x)));

		public void Set(params Status[] statuses)
			=> Set(statuses as IEnumerable<Status>);

		public void Set(IEnumerable<Status> statuses)
		{
			Status updateWith = null;
			lock (ActiveStatusesLock)
			{
				var updated = false;

				foreach (var status in statuses)
				{
					if (status.IsCompleted)
					{
						updated = ActiveStatuses.RemoveWhere(x => x.Type == status.Type) == 1 || updated;
					}
					else
					{
						var found = ActiveStatuses.FirstOrDefault(x => x.Type == status.Type);
						if (found is { Percentage: var perc })
						{
							if (perc != status.Percentage)
							{
								ActiveStatuses.Remove(found);
								updated = ActiveStatuses.Add(status) || updated;
							}
						}
						else
						{
							updated = ActiveStatuses.Add(status) || updated;
						}
					}
				}

				if (updated && ActiveStatuses.Any())
				{
					var priority = ActiveStatuses.Min(x => x.Type);
					updateWith = ActiveStatuses.First(x => x.Type == priority);
				}
			}

			if (updateWith is { })
			{
				Dispatcher.UIThread.PostLogException(() => CurrentStatus = updateWith);
			}
		}
	}
}
