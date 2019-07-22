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
			AddHandler(PointerPressedEvent, (sender, e) =>
			{
				if (e.MouseButton == MouseButton.Left || e.MouseButton == MouseButton.Right)
				{
					UpdateSelectionFromEventSource(
						e.Source,
						true,
						(e.InputModifiers & InputModifiers.Shift) != 0,
						(e.InputModifiers & InputModifiers.Control) != 0);
				}
			}, RoutingStrategies.Tunnel, true);
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
		}
	}
}
