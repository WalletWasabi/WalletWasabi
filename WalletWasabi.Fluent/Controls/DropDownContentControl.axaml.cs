using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;

public class DropDownContentControl : ContentControl
{
	public static readonly StyledProperty<IEnumerable> MenuItemsProperty = AvaloniaProperty.Register<DropDownContentControl, IEnumerable>("MenuItems");

	public static readonly StyledProperty<IBrush> DropDownButtonBrushProperty = AvaloniaProperty.Register<DropDownContentControl, IBrush>(nameof(DropDownButtonBrush));

	public IEnumerable MenuItems
	{
		get => GetValue(MenuItemsProperty);
		set => SetValue(MenuItemsProperty, value);
	}

	public IBrush DropDownButtonBrush
	{
		get => GetValue(DropDownButtonBrushProperty);
		set => SetValue(DropDownButtonBrushProperty, value);
	}
}
