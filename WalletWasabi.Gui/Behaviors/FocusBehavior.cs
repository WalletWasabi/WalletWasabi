using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
	internal class FocusBehavior : Behavior<Control>
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		private static readonly AvaloniaProperty<bool> IsFocusedProperty =
			AvaloniaProperty.Register<FocusBehavior, bool>(nameof(IsFocused), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

		public bool IsFocused
		{
			get => GetValue(IsFocusedProperty);
			set => SetValue(IsFocusedProperty, value);
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			AssociatedObject.AttachedToLogicalTree += (sender, e) =>
			{
				Disposables.Add(this.GetObservable(IsFocusedProperty).Subscribe(focused =>
				{
					if (focused)
					{
						AssociatedObject.Focus();
					}
				}));
			};
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
