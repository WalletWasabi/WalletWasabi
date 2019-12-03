using Avalonia.Controls.Notifications;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Concurrency;

namespace WalletWasabi.Gui.Helpers
{
	public static class NotificationHelpers
	{
		private static INotificationManager NullNotificationManager { get; } = new NullNotificationManager();

		public static INotificationManager GetNotificationManager()
		{
			return Locator.Current.GetService<INotificationManager>() ?? NullNotificationManager;
		}

		public static void Notify(string message, string title, NotificationType type, Action onClick = null)
		{
			using var disposable = RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, type, TimeSpan.FromSeconds(7), onClick)));
		}

		public static void Success(string message, string title = "Success!")
		{
			using var disposable = RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Success, TimeSpan.FromSeconds(7))));
		}

		public static void Information(string message, string title = "Info")
		{
			using var disposable = RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Information, TimeSpan.FromSeconds(7))));
		}

		public static void Warning(string message, string title = "Warning!")
		{
			using var disposable = RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Warning, TimeSpan.FromSeconds(7))));
		}

		public static void Error(string message, string title = "Error")
		{
			using var disposable = RxApp.MainThreadScheduler
				.Schedule(() => GetNotificationManager()
				.Show(new Notification(title, message, NotificationType.Error, TimeSpan.FromSeconds(7))));
		}
	}
}
