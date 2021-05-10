using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors
{
	public class RegisterNotificationHostBehavior : DisposingBehavior<Window>
	{
		protected override void OnAttached(CompositeDisposable disposables)
		{
			if (AssociatedObject is null)
			{
				return;
			}

			NotificationHelpers.SetNotificationManager(AssociatedObject);

			// Must set notification host again after theme changing.
			// TODO: improve, execute only once a time
			Observable
				.FromEventPattern(AssociatedObject, nameof(AssociatedObject.ResourcesChanged))
				.Subscribe(_ => NotificationHelpers.SetNotificationManager(AssociatedObject))
				.DisposeWith(disposables);
		}
	}
}
