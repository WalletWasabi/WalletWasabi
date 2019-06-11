using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Text;

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
