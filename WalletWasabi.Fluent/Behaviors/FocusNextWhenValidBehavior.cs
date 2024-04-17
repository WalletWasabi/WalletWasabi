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
	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var hasErrors = AssociatedObject.GetObservable(DataValidationErrors.HasErrorsProperty).Skip(1);
		var text = AssociatedObject.GetObservable(TextBox.TextProperty).Skip(1);

		hasErrors.ToSignal()
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
			})
			.DisposeWith(disposables);
	}
}
