namespace WalletWasabi.Gui.ViewModels
{
	public class Notification
	{
		public Notification(NotificationTypeEnum notificationType, string notificationText, bool unattended)
		{
			NotificationType = notificationType;
			NotificationText = notificationText;
			Unattended = unattended;
		}

		public NotificationTypeEnum NotificationType { get; }
		public string NotificationText { get; }
		public bool Unattended { get; }
	}
}
