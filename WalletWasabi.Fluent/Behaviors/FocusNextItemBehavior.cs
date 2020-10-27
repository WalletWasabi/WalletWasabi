using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	internal class FocusNextItemBehavior : Behavior<Control>
	{
		private IDisposable _disposable;

		public static readonly StyledProperty<bool> IsFocusedProperty =
			AvaloniaProperty.Register<FocusNextItemBehavior, bool>(nameof(IsFocused), true);

		public bool IsFocused
		{
			get => GetValue(IsFocusedProperty);
			set => SetValue(IsFocusedProperty, value);
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			_disposable?.Dispose();

			_disposable = this.WhenAnyValue(x => x.IsFocused)
				.Subscribe(x =>
				{
					if (!x)
					{
						var nextToFocus = KeyboardNavigationHandler.GetNext(AssociatedObject, NavigationDirection.Next);
						nextToFocus.Focus();
					}
				});
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			_disposable?.Dispose();
		}
	}
}