using Avalonia.Controls.Notifications;

namespace WalletWasabi.Gui.Helpers
{
	public class NullNotificationManager : INotificationManager
	{
		public void Show(INotification notification)
		{
		}
	}
}
