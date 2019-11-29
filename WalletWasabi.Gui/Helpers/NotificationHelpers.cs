using Avalonia.Controls.Notifications;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Concurrency;

namespace WalletWasabi.Gui.Helpers
{
	public static class NotificationHelpers
	{
		public static INotificationManager GetNotificationManager()
		{
			return Locator.Current.GetService<INotificationManager>();
		}

		public static void Notify(string message, string title, NotificationType type, Action onClick = null)
		{
			RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, type, TimeSpan.FromSeconds(7), onClick)));
		}

		public static void Success(string message, string title = "Success!")
		{
			RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Success, TimeSpan.FromSeconds(7))));
		}

		public static void Information(string message, string title = "Info")
		{
			RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Information, TimeSpan.FromSeconds(7))));
		}

		public static void Warning(string message, string title = "Warning!")
		{
			RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Warning, TimeSpan.FromSeconds(7))));
		}

		public static void Error(string message, string title = "Error")
		{
			RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Error, TimeSpan.FromSeconds(7))));
		}
	}
}
