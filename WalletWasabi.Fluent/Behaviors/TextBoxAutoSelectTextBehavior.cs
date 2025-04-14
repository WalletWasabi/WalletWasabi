using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

internal class TextBoxAutoSelectTextBehavior : AttachedToVisualTreeBehavior<TextBox>
{
	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		var gotFocus = AssociatedObject.OnEvent(InputElement.GotFocusEvent);
		var lostFocus = AssociatedObject.OnEvent(InputElement.LostFocusEvent);
		var isFocused = gotFocus.Select(_ => true).Merge(lostFocus.Select(_ => false));

		return isFocused
			.Throttle(TimeSpan.FromSeconds(0.1))
			.DistinctUntilChanged()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Where(focused => focused)
			.Do(_ => AssociatedObject.SelectAll())
			.Subscribe();
	}
}
