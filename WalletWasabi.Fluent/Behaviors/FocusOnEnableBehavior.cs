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
				.Where(x => x.EventArgs.Property.Name == nameof(AssociatedObject.IsEffectivelyEnabled) && x.EventArgs.NewValue is { } newValue && (bool)newValue == true)
				.Subscribe(_ => AssociatedObject!.Focus());
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();
		}
	}
}