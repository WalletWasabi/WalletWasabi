using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors
{
	public class RegisterNotificationHostBehavior : AttachedToVisualTreeBehavior<Window>
	{
		protected override void OnAttachedToVisualTree()
		{
			var notificationManager = new WindowNotificationManager(AssociatedObject)
			{
				Position = NotificationPosition.BottomRight,
				MaxItems = 4,
				Margin = new Thickness(0, 0, 15, 40)
			};

			NotificationHelpers.SetNotificationManager(notificationManager);
		}
	}
}
