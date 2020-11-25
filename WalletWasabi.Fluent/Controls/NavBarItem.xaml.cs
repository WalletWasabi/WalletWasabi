using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Container for NavBarItems.
	/// </summary>
	public class NavBarItem : ListBoxItem
	{
		public static readonly StyledProperty<IconElement> IconProperty =
			AvaloniaProperty.Register<NavBarItem, IconElement>(nameof(Icon));

		public static readonly StyledProperty<Orientation> IndicatorOrientationProperty =
			AvaloniaProperty.Register<NavBarItem, Orientation>(nameof(IndicatorOrientation), Orientation.Vertical);

		/// <summary>
		/// The icon to be shown beside the header text of the item.
		/// </summary>
		public IconElement Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}

		/// <summary>
		/// Gets or sets the indicator orientation.
		/// </summary>
		public Orientation IndicatorOrientation
		{
			get => GetValue(IndicatorOrientationProperty);
			set => SetValue(IndicatorOrientationProperty, value);
		}
	}
}
