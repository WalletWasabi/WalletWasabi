using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Controls;

public class PrivacyBarSegment : TemplatedControl
{
	public static readonly StyledProperty<decimal> AmountProperty =
		AvaloniaProperty.Register<PrivacyBarSegment, decimal>(nameof(Amount));

	public static readonly StyledProperty<double> StartProperty =
		AvaloniaProperty.Register<PrivacyBarSegment, double>(nameof(Start));

	public static readonly StyledProperty<double> BarWidthProperty =
		AvaloniaProperty.Register<PrivacyBarSegment, double>(nameof(BarWidth));

	public static readonly StyledProperty<Geometry> DataProperty =
		AvaloniaProperty.Register<PrivacyBarSegment, Geometry>(nameof(Data));

	public static readonly StyledProperty<PrivacyLevel> PrivacyLevelProperty =
		AvaloniaProperty.Register<PrivacyBarSegment, PrivacyLevel>(nameof(PrivacyLevel));

	static PrivacyBarSegment()
	{
		AffectsArrange<PrivacyBarSegment>(StartProperty);
		AffectsArrange<PrivacyBarSegment>(BarWidthProperty);
	}

	public decimal Amount
	{
		get => GetValue(AmountProperty);
		set => SetValue(AmountProperty, value);
	}

	public double Start
	{
		get => GetValue(StartProperty);
		set => SetValue(StartProperty, value);
	}

	public double BarWidth
	{
		get => GetValue(BarWidthProperty);
		set => SetValue(BarWidthProperty, value);
	}

	public Geometry Data
	{
		get => GetValue(DataProperty);
		set => SetValue(DataProperty, value);
	}

	public PrivacyLevel PrivacyLevel
	{
		get => GetValue(PrivacyLevelProperty);
		set => SetValue(PrivacyLevelProperty, value);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		var size = base.ArrangeOverride(finalSize);
		Data = new RectangleGeometry(new Rect(Start, 0, BarWidth, size.Height));
		return size;
	}
}
