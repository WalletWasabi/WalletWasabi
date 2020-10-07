using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Container for NavBarItems.
	/// </summary>
	public class NavBarItem : ListBoxItem
	{
		public static readonly StyledProperty<IconElement> IconProperty =
			AvaloniaProperty.Register<NavBarItem, IconElement>(nameof(Icon));

		/// <summary>
		/// The icon to be shown beside the header text of the item.
		/// </summary>
		public IconElement Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}
	}
}
