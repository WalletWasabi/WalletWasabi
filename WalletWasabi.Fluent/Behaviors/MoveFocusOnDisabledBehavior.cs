using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.Behaviors
{
	public class MoveFocusOnDisabledBehavior : Behavior<Control>
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		protected override void OnAttached()
		{
			base.OnAttached();

			AssociatedObject.PropertyChanged += AssociatedObject_PropertyChanged;
		}

		private void AssociatedObject_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property.Name == "IsEnabled")
			{

			}
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
