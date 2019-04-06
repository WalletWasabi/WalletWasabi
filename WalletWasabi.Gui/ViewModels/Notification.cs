namespace WalletWasabi.Gui.ViewModels
{
	public class Notification
	{
		public Notification(NotificationTypeEnum notificationType, string notificationText, bool unattended, bool duplicated)
		{
			NotificationType = notificationType;
			NotificationText = notificationText;
			Unattended = unattended;
			Duplicated = duplicated;
		}

		public NotificationTypeEnum NotificationType { get; }
		public string NotificationText { get; }
		public bool Unattended { get; }
		public bool Duplicated { get; }
	}
}
