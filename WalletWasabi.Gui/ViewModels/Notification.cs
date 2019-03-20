namespace WalletWasabi.Gui.ViewModels
{
	public class Notification
	{
		public Notification(NotificationTypeEnum notificationType, string notificationText)
		{
			NotificationType = notificationType;
			NotificationText = notificationText;
		}

		public NotificationTypeEnum NotificationType { get; }
		public string NotificationText { get; }
	}
}
