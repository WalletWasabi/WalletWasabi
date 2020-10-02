using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
	public class NavBarItem : ListBoxItem
	{
		public static readonly StyledProperty<IconElement> IconProperty =
			AvaloniaProperty.Register<NavBarItem, IconElement>(nameof(Icon));

		public static readonly StyledProperty<string> HeaderProperty =
			AvaloniaProperty.Register<NavBarItem, string>(nameof(Header));		

		/// <summary>
		/// The icon to be shown beside the header text of the item.
		/// </summary>
		public IconElement Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}

		/// <summary>
		/// The header text of the item.
		/// </summary>
		public string Header
		{
			get => GetValue(HeaderProperty);
			set => SetValue(HeaderProperty, value);
		}
	}
}
