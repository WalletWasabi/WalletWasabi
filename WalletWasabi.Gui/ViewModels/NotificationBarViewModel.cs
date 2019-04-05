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

		public ReactiveCommand CloseCommand { get; } 

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

		private bool _displaying;

		public bool Displaying
		{
			get => _displaying;
			set => this.RaiseAndSetIfChanged(ref _displaying, value);
		}

	
		public NotificationBarViewModel()
		{
			Global.NotificationManager.NotificationAvailable += (s, n) => _notificationQueue.Enqueue(n);

			CloseCommand = ReactiveCommand.Create(()=>Displaying = false);

			Task.Run(async () =>
			{
				while (true)
				{
					if (_notificationQueue.Any() && !Displaying)
					{
						var notification = _notificationQueue.Dequeue();
						
						await Dispatcher.UIThread.InvokeAsync(()=>DisplayNotification(notification));
					}
					await Task.Delay(TimeSpan.FromSeconds(0.1));
				}
			});

			Displaying = false;
		}

		private void DisplayNotification(Notification notification)
		{
			NotificationText = notification.NotificationText;
			NotificationType = notification.NotificationType;
			Displaying = true;
		}
	}
}
