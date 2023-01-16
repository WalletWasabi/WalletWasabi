using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Views.NavBar;

public class NavBarBox : SelectingItemsControl
{
	protected override void OnGotFocus(GotFocusEventArgs e)
	{
		base.OnGotFocus(e);

		if (e.NavigationMethod == NavigationMethod.Directional)
		{
			e.Handled = UpdateSelectionFromEventSource(
				e.Source,
				true,
				e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
				e.KeyModifiers.HasAllFlags(KeyModifiers.Control));
		}
	}

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);

		if (e.Source is IVisual source)
		{
			var point = e.GetCurrentPoint(source);

			if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
			{
				e.Handled = UpdateSelectionFromEventSource(
					e.Source,
					true,
					e.KeyModifiers.HasAllFlags(KeyModifiers.Shift),
					e.KeyModifiers.HasAllFlags(AvaloniaLocator.Current.GetRequiredService<PlatformHotkeyConfiguration>().CommandModifiers),
					point.Properties.IsRightButtonPressed);
			}
		}
	}

}
