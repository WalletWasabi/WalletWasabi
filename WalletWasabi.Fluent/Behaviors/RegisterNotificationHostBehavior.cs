using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors
{
	public class RegisterNotificationHostBehavior : Behavior<Window>
	{
		protected override void OnAttached()
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
