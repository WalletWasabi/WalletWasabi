using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Controls;
public class DropDownContentControl : ContentControl
{
	private IEnumerable _menuItems;

	public static readonly DirectProperty<DropDownContentControl, IEnumerable> MenuItemsProperty = AvaloniaProperty.RegisterDirect<DropDownContentControl, IEnumerable>(
		"MenuItems",
		o => o.MenuItems,
		(o, v) => o.MenuItems = v);

	public IEnumerable MenuItems
	{
		get => _menuItems;
		set => SetAndRaise(MenuItemsProperty, ref _menuItems, value);
	}

	public static readonly StyledProperty<IBrush> DropDownButtonBrushProperty = AvaloniaProperty.Register<DropDownContentControl, IBrush>("DropDownButtonBrush");

	public IBrush DropDownButtonBrush
	{
		get => GetValue(DropDownButtonBrushProperty);
		set => SetValue(DropDownButtonBrushProperty, value);
	}
}
