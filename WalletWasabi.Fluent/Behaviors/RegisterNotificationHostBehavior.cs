using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Xaml.Interactions.Custom;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class RegisterNotificationHostBehavior : AttachedToVisualTreeBehavior<Visual>
{
	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		NotificationHelpers.SetNotificationManager(AssociatedObject);

		return Disposable.Empty;
	}
}
