using System;
using System.Reactive.Concurrency;
using Avalonia.Controls.Notifications;
using ReactiveUI;

namespace WalletWasabi.Fluent.Helpers
{
	public static class NotificationHelpers
	{
		private const int DefaultNotificationTimeout = 7;
		private static WindowNotificationManager? NotificationManager;

		public static void SetNotificationManager(WindowNotificationManager windowNotificationManager)
		{
			if (NotificationManager is { })
			{
				throw new InvalidOperationException("Already set!");
			}

			NotificationManager = windowNotificationManager;
		}

		public static void Show(string title, string message)
		{
			if (NotificationManager is { } nm)
			{
				RxApp.MainThreadScheduler.Schedule(() => nm.Show(new Notification(title, message, NotificationType.Information, TimeSpan.FromSeconds(DefaultNotificationTimeout))));
			}
		}
	}
}
