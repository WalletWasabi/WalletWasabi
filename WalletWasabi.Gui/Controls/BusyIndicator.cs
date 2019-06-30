using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using System;

namespace WalletWasabi.Gui.Controls
{
	public class BusyIndicator : ContentControl, IStyleable
	{
		Type IStyleable.StyleKey => typeof(BusyIndicator);

		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<BusyIndicator, string>(nameof(Text));

		public static readonly StyledProperty<bool> IsBusyProperty =
			AvaloniaProperty.Register<BusyIndicator, bool>(nameof(IsBusy));

		public string Text
		{
			get => GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		public bool IsBusy
		{
			get => GetValue(IsBusyProperty);
			set => SetValue(IsBusyProperty, value);
		}
	}
}
