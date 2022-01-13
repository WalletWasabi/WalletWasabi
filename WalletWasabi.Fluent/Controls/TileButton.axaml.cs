using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class TileButton : Button
{
	public static readonly StyledProperty<int> IconSizeProperty =
		AvaloniaProperty.Register<TileButton, int>(nameof(IconSize), 40);

	public static readonly StyledProperty<string> TextProperty =
		AvaloniaProperty.Register<TileButton, string>(nameof(Text));

	public static readonly StyledProperty<Geometry> IconProperty =
		AvaloniaProperty.Register<TileButton, Geometry>(nameof(Icon));

	public int IconSize
	{
		get => GetValue(IconSizeProperty);
		set => SetValue(IconSizeProperty, value);
	}

	public string Text
	{
		get => GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public Geometry Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}
}
