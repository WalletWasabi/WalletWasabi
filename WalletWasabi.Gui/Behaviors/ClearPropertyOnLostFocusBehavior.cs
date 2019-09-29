using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Behaviors
{
	public class ClearPropertyOnLostFocusBehavior : Behavior<Control>
	{
		private CompositeDisposable Disposables { get; set; }

		public static readonly AvaloniaProperty<object> TargetPropertyProperty =
			AvaloniaProperty.Register<ClearPropertyOnLostFocusBehavior, object>(nameof(TargetProperty), defaultBindingMode: BindingMode.TwoWay);

		public object TargetProperty
		{
			get => GetValue(TargetPropertyProperty);
			set => SetValue(TargetPropertyProperty, value);
		}

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable
			{
				Observable.FromEventPattern<RoutedEventArgs>(AssociatedObject, nameof(AssociatedObject.LostFocus))
					.Subscribe(_=> TargetProperty = null)
			};

			base.OnAttached();
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
