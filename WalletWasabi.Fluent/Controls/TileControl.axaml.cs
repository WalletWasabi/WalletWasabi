using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class TileControl : ContentControl
{
	public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<TileControl, string>("Title");

	public string Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public static readonly StyledProperty<object> BottomContentProperty = AvaloniaProperty.Register<TileControl, object>("BottomContent");

	public object BottomContent
	{
		get => GetValue(BottomContentProperty);
		set => SetValue(BottomContentProperty, value);
	}

	public static readonly StyledProperty<bool> IsBottomContentVisibleProperty = AvaloniaProperty.Register<TileControl, bool>("IsBottomContentVisible", true);

	public bool IsBottomContentVisible
	{
		get => GetValue(IsBottomContentVisibleProperty);
		set => SetValue(IsBottomContentVisibleProperty, value);
	}

	public static readonly StyledProperty<Thickness> SeparatorMarginProperty = AvaloniaProperty.Register<TileControl, Thickness>("SeparatorMargin");

	public Thickness SeparatorMargin
	{
		get => GetValue(SeparatorMarginProperty);
		set => SetValue(SeparatorMarginProperty, value);
	}

	public static readonly StyledProperty<double> BottomPartHeightProperty = AvaloniaProperty.Register<TileControl, double>("BottomPartHeight");

	public double BottomPartHeight
	{
		get => GetValue(BottomPartHeightProperty);
		set => SetValue(BottomPartHeightProperty, value);
	}
}
