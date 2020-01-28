using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using System;

namespace WalletWasabi.Gui.Controls
{
	public class ExtendedListBox : ListBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(ListBox);

		public ExtendedListBox()
		{
			AddHandler(PointerPressedEvent,
				(sender, e) =>
				{
					var properties = e.GetCurrentPoint(this).Properties;

					if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed)
					{
						UpdateSelectionFromEventSource(
							e.Source,
							true,
							e.KeyModifiers.HasFlag(KeyModifiers.Shift),
							e.KeyModifiers.HasFlag(KeyModifiers.Control));
					}
				},
				RoutingStrategies.Tunnel, true);
		}
	}
}
