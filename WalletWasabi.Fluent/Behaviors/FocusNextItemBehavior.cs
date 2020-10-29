using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	internal class FocusNextItemBehavior : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<bool> IsFocusedProperty =
			AvaloniaProperty.Register<FocusNextItemBehavior, bool>(nameof(IsFocused), true);

		public bool IsFocused
		{
			get => GetValue(IsFocusedProperty);
			set => SetValue(IsFocusedProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			this.WhenAnyValue(x => x.IsFocused)
				.Where(x => x == false)
				.Subscribe(
					_ =>
				{
					KeyboardNavigationHandler.GetNext(AssociatedObject!, NavigationDirection.Next)?.Focus();
				})
				.DisposeWith(disposables);
		}
	}
}