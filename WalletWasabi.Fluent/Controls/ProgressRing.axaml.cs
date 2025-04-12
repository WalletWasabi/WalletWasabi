using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class ProgressRing : TemplatedControl
{
	public static readonly StyledProperty<bool> IsIndeterminateProperty =
		AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsIndeterminate));

	public static readonly StyledProperty<double> PercentageProperty =
		AvaloniaProperty.Register<ProgressRing, double>(nameof(Percentage));

	public static readonly StyledProperty<double> StrokeThicknessProperty =
		AvaloniaProperty.Register<ProgressRing, double>(nameof(StrokeThickness), 6);

	public static readonly StyledProperty<double> StrokeBorderThicknessProperty =
		AvaloniaProperty.Register<ProgressRing, double>(nameof(StrokeThickness));

	public static readonly StyledProperty<IBrush> StrokeBorderBrushProperty =
		AvaloniaProperty.Register<ProgressRing, IBrush>(nameof(StrokeBorderBrush));

	public bool IsIndeterminate
	{
		get => GetValue(IsIndeterminateProperty);
		set => SetValue(IsIndeterminateProperty, value);
	}

	public double Percentage
	{
		get => GetValue(PercentageProperty);
		set => SetValue(PercentageProperty, value);
	}

	public double StrokeThickness
	{
		get => GetValue(StrokeThicknessProperty);
		set => SetValue(StrokeThicknessProperty, value);
	}

	public double StrokeBorderThickness
	{
		get => GetValue(StrokeBorderThicknessProperty);
		set => SetValue(StrokeBorderThicknessProperty, value);
	}

	public IBrush StrokeBorderBrush
	{
		get => GetValue(StrokeBorderBrushProperty);
		set => SetValue(StrokeBorderBrushProperty, value);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);

		if (e.Property == IsIndeterminateProperty)
		{
			PseudoClasses.Set(":indeterminate", IsIndeterminate);
		}
	}
}
