using System;
using Avalonia.Controls.Notifications;

namespace WalletWasabi.Fluent.Helpers
{
	public static class NotificationHelpers
	{
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
				nm.Show(new Notification(title, message));
			}
		}
	}
}
