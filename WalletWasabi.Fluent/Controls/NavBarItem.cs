using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Template class for items to be displayed in the sidebar.
	/// </summary>
	public class NavBarItem : TemplatedControl
	{
		/// <summary>
		/// Defines the <see cref="IsSelected"/> property.
		/// </summary>
		public static readonly StyledProperty<bool> IsSelectedProperty =
			AvaloniaProperty.Register<NavBarItem, bool>(nameof(IsSelected));

		/// <summary>
		/// Defines the <see cref="Icon"/> property.
		/// </summary>
		public static readonly StyledProperty<IconElement> IconProperty =
			AvaloniaProperty.Register<NavBarItem, IconElement>(nameof(Icon));

		/// <summary>
		/// Defines the <see cref="Header"/> property.
		/// </summary>
		public static readonly StyledProperty<string> HeaderProperty =
			AvaloniaProperty.Register<NavBarItem, string>(nameof(Header));

		/// <summary>
		/// Initializes the static members of the <see cref="NavBarItem"/> class.
		/// </summary>
		static NavBarItem()
		{
			SelectableMixin.Attach<NavBarItem>(IsSelectedProperty);
			PressedMixin.Attach<NavBarItem>();
			FocusableProperty.OverrideDefaultValue<NavBarItem>(true);
		}

		/// <summary>
		/// Gets or sets the selection state of the item.
		/// </summary>
		public bool IsSelected
		{
			get => GetValue(IsSelectedProperty);
			set => SetValue(IsSelectedProperty, value);
		}

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
