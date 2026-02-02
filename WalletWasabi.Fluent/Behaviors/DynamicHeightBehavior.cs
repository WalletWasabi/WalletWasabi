using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class DynamicHeightBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<double> HeightMultiplierProperty =
		AvaloniaProperty.Register<DynamicHeightBehavior, double>(nameof(HeightMultiplier));

	public static readonly StyledProperty<double> HideThresholdHeightProperty =
		AvaloniaProperty.Register<DynamicHeightBehavior, double>(nameof(HideThresholdHeight));

	public double HeightMultiplier
	{
		get => GetValue(HeightMultiplierProperty);
		set => SetValue(HeightMultiplierProperty, value);
	}

	public double HideThresholdHeight
	{
		get => GetValue(HideThresholdHeightProperty);
		set => SetValue(HideThresholdHeightProperty, value);
	}

	protected override IDisposable OnAttachedOverride()
	{
		if (AssociatedObject?.Parent is not Control parent)
		{
			return Disposable.Empty;
		}

		return parent.WhenAnyValue(x => x.Bounds)
			.Subscribe(bounds =>
			{
				var newHeight = bounds.Height * HeightMultiplier;

				if (newHeight < HideThresholdHeight)
				{
					AssociatedObject.IsVisible = false;
				}
				else
				{
					AssociatedObject.IsVisible = true;
					AssociatedObject.Height = newHeight;
				}
			});
	}
}
