using System;
using System.Collections.Generic;
using NBitcoin.Protocol;
using ReactiveUI;
using WalletWasabi.Services;
using Avalonia.Data.Converters;
using System.Globalization;
using WalletWasabi.Models;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using WalletWasabi.Gui.Tabs;
using System.Reactive.Linq;
using WalletWasabi.Gui.Dialogs;
using System.Runtime.InteropServices;
using System.Reactive.Disposables;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;

namespace WalletWasabi.Gui.ViewModels
{
	public class NotificationBarViewModel : ViewModelBase
	{
		private Queue<Notification> _notificationQueue = new Queue<Notification>();
		private ObservableCollection<NotificationViewModel> _notifications;
		public ObservableCollection<NotificationViewModel> Notifications
		{
			get => _notifications;
			set => this.RaiseAndSetIfChanged(ref _notifications, value);
		}
	
		public NotificationBarViewModel()
		{
			Notifications = new ObservableCollection<NotificationViewModel>();
			Global.NotificationManager.NotificationAvailable += (s, n) => _notificationQueue.Enqueue(n);

			Task.Run(async () =>
			{
				while (true)
				{
					if (_notificationQueue.Any())
					{
						var notification = _notificationQueue.Dequeue();
						if(!notification.Duplicated)
						{
							await DisplayNotificationAsync(notification);
						}
						else
						{
							Notifications.Last().IncreaseCounter();
						}
					}
					await Task.Delay(TimeSpan.FromSeconds(0.1));
				}
			});
		}

		private async Task DisplayNotificationAsync(Notification notification)
		{
			await Dispatcher.UIThread.InvokeAsync(()=>{
				Notifications.Add(new NotificationViewModel(this){
					NotificationText = notification.NotificationText,
					NotificationType = notification.NotificationType,
					Displaying = true 
				});
			});
		}
	}
}
