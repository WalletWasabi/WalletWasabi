using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.Behaviors;

namespace WalletWasabi.Fluent;

public class AttachedFlyoutDismissedTriggerBehavior : DisposingTrigger
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is not Control associatedObject)
		{
			return;
		}

		var flyout = FlyoutBase.GetAttachedFlyout(associatedObject);

		if (flyout is null)
		{
			return;
		}

		Observable.FromEventPattern(h => flyout.Closed += h, h => flyout.Closed -= h)
			.Do(_ => Interaction.ExecuteActions(AssociatedObject, Actions, null))
			.Subscribe()
			.DisposeWith(disposables);
	}
}
