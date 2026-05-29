using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.Custom;
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
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(_ =>
			{
				if (AssociatedObject is { } tb &&
					!DataValidationErrors.GetHasErrors(AssociatedObject) &&
					!string.IsNullOrEmpty(AssociatedObject.Text) &&
					TopLevel.GetTopLevel(tb)?.FocusManager is { } focusManager)
				{
					var options = new FindNextElementOptions() { FocusedElement = tb };
					focusManager.TryMoveFocus(NavigationDirection.Next, options);
				}
			});
	}
}
