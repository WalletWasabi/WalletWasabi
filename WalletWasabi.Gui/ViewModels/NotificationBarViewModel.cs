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

namespace WalletWasabi.Gui.ViewModels
{
	public class NotificationBarViewModel : ViewModelBase
	{
		private Queue<Notification> _notificationQueue = new Queue<Notification>();
		private DispatcherTimer _timer;
		private string _notificationText;

		public string NotificationText
		{
			get => _notificationText;
			set => this.RaiseAndSetIfChanged(ref _notificationText, value);
		}

		private NotificationTypeEnum _notificationType;

		public NotificationTypeEnum NotificationType
		{
			get => _notificationType;
			set => this.RaiseAndSetIfChanged(ref _notificationType, value);
		}

		public NotificationBarViewModel()
		{
			Global.NotificationManager.NotificationAvailable += (s, n) => _notificationQueue.Enqueue(n);
			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(3)
			};

			_timer.Tick += (sender, e) =>
			{
				_timer.Stop();
				NotificationText = string.Empty;
				NotificationType = NotificationTypeEnum.None;
			};

			Task.Run(async () =>
			{
				while (true)
				{
					if (_notificationQueue.Any() && !_timer.IsEnabled)
					{
						var notification = _notificationQueue.Dequeue();
						
						await Dispatcher.UIThread.InvokeAsync(()=>DisplayNotification(notification));
					}
					await Task.Delay(TimeSpan.FromSeconds(0.1));
				}
			});
		}

		private void DisplayNotification(Notification notification)
		{
			NotificationText = notification.NotificationText;
			NotificationType = notification.NotificationType;
			_timer.Start();
		}
	}
}
