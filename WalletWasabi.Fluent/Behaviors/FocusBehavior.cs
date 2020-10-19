using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.Behaviors
{
	public class FocusBehavior : Behavior<Control>
	{
		public static readonly StyledProperty<bool> IsFocusedProperty =
		StyledProperty<bool>.Register<FocusBehavior, bool>(nameof(IsFocused), defaultBindingMode: BindingMode.TwoWay);

		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		public bool IsFocused
		{
			get => GetValue(IsFocusedProperty);
			set => SetValue(IsFocusedProperty, value);
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			AssociatedObject.AttachedToLogicalTree += (sender, e) =>
				Disposables.Add(this.GetObservable(IsFocusedProperty)
					.Subscribe(focused =>
					{
						if (focused)
						{
							AssociatedObject.Focus();
						}
					}));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
