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

		public static void Information (string title, string message)
		{
			GetNotificationManager().Show(new Notification(title, message, NotificationType.Information));
		}

		public static void Warning (string title, string message)
		{
			GetNotificationManager().Show(new Notification(title, message, NotificationType.Warning));
		}

		public static void Error(string title, string message)
		{
			GetNotificationManager().Show(new Notification(title, message, NotificationType.Error));
		}
	}
}
