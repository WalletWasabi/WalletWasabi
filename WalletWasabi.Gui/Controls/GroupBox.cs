using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls
{
	public class GroupBox : ContentControl
	{
		public static readonly StyledProperty<object> TitleProperty =
			AvaloniaProperty.Register<GroupBox, object>(nameof(Title));

		public object Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}
	}
}
