using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Xaml.Interactions.Custom;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.Behaviors;

public class BrushSwitchBehavior : AttachedToVisualTreeBehavior<ProgressBar>
{
	public static readonly StyledProperty<bool> ValueProperty =
		AvaloniaProperty.Register<BrushSwitchBehavior, bool>(nameof(Value));

	public static readonly StyledProperty<IBrush?> FalseBrushProperty =
		AvaloniaProperty.Register<BrushSwitchBehavior, IBrush?>(nameof(FalseBrush));

	public static readonly StyledProperty<IBrush?> TrueBrushProperty =
		AvaloniaProperty.Register<BrushSwitchBehavior, IBrush?>(nameof(TrueBrush));

	public bool Value
	{
		get => GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public IBrush? FalseBrush
	{
		get => GetValue(FalseBrushProperty);
		set => SetValue(FalseBrushProperty, value);
	}

	public IBrush? TrueBrush
	{
		get => GetValue(TrueBrushProperty);
		set => SetValue(TrueBrushProperty, value);
	}

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		UpdateForeground();

		return this.GetObservable(ValueProperty)
			.Subscribe(_ => UpdateForeground());
	}

	private void UpdateForeground()
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject.Foreground = Value ? TrueBrush : FalseBrush;
	}
}
