using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

internal class BoundsObserverBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<Rect> BoundsProperty =
		AvaloniaProperty.Register<BoundsObserverBehavior, Rect>(nameof(Bounds), defaultBindingMode: BindingMode.OneWay);

	public static readonly StyledProperty<double> WidthProperty =
		AvaloniaProperty.Register<BoundsObserverBehavior, double>(nameof(Width), defaultBindingMode: BindingMode.TwoWay);

	public static readonly StyledProperty<double> HeightProperty =
		AvaloniaProperty.Register<BoundsObserverBehavior, double>(nameof(Height), defaultBindingMode: BindingMode.TwoWay);

	public Rect Bounds
	{
		get => GetValue(BoundsProperty);
		set => SetValue(BoundsProperty, value);
	}

	public double Width
	{
		get => GetValue(WidthProperty);
		set => SetValue(WidthProperty, value);
	}

	public double Height
	{
		get => GetValue(HeightProperty);
		set => SetValue(HeightProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is not null)
		{
			disposables.Add(this.GetObservable(BoundsProperty)
				.Subscribe(bounds =>
				{
					Width = bounds.Width;
					Height = bounds.Height;
				}));
		}
	}
}
