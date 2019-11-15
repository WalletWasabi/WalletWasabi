using Avalonia.Controls.Notifications;
using Splat;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Helpers
{
	public static class NotificationHelpers
	{
		public static INotificationManager GetNotificationManager()
		{
			return Locator.Current.GetService<INotificationManager>();
		}

		public static void Success (string message, string title = "Success!")
		{
			GetNotificationManager().Show(new Notification(title, message, NotificationType.Success));
		}

		public static void Information (string message, string title = "Info!")
		{
			GetNotificationManager().Show(new Notification(title, message, NotificationType.Warning));
		}

		public static void Warning (string message, string title = "Warning!")
		{
			GetNotificationManager().Show(new Notification(title, message, NotificationType.Warning));
		}

		public static void Error(string message, string title = "Error!")
		{
			GetNotificationManager().Show(new Notification(title, message, NotificationType.Error));
		}
	}
}
