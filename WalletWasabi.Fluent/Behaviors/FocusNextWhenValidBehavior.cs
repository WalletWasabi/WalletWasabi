using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class FocusNextWhenValidBehavior : AttachedToVisualTreeBehavior<TextBox>
{
	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		var hasErrors = AssociatedObject.GetObservable(DataValidationErrors.HasErrorsProperty);
		var text = AssociatedObject.GetObservable(TextBox.TextProperty);

		return hasErrors.ToSignal()
			.Merge(text.ToSignal())
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				if (AssociatedObject is { } &&
					!DataValidationErrors.GetHasErrors(AssociatedObject) &&
					!string.IsNullOrEmpty(AssociatedObject.Text) &&
					KeyboardNavigationHandler.GetNext(AssociatedObject, NavigationDirection.Next) is
					{ } nextFocus)
				{
					nextFocus.Focus();
				}
			});
	}
}
