using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Behaviors
{
	public class FocusOnEnableBehavior : Behavior<Control>
	{
		protected override void OnAttached()
		{
			base.OnAttached();

			Observable.
				FromEventPattern<AvaloniaPropertyChangedEventArgs>(AssociatedObject, nameof(AssociatedObject.PropertyChanged))
				.Select(x => x.EventArgs)
				.Where(x => x.Property.Name == nameof(AssociatedObject.IsEffectivelyEnabled) && x.NewValue is { } && (bool)x.NewValue == true)
				.Subscribe(_ => AssociatedObject!.Focus());
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();
		}
	}
}