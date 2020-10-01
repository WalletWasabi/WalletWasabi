using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
	public class NavBarItem : TemplatedControl
	{
		/// <summary>
		/// Defines the <see cref="IsSelected"/> property.
		/// </summary>
		public static readonly StyledProperty<bool> IsSelectedProperty =
			AvaloniaProperty.Register<NavBarItem, bool>(nameof(IsSelected));

		public static readonly StyledProperty<IconElement> IconProperty =
			AvaloniaProperty.Register<NavBarItem, IconElement>(nameof(Icon));

		public static readonly StyledProperty<string> HeaderProperty =
			AvaloniaProperty.Register<NavBarItem, string>(nameof(Header));

		/// <summary>
		/// Initializes static members of the <see cref="NavBarItem"/> class.
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
			get { return GetValue(IsSelectedProperty); }
			set { SetValue(IsSelectedProperty, value); }
		}

		public IconElement Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}

		public string Header
		{
			get => GetValue(HeaderProperty);
			set => SetValue(HeaderProperty, value);
		}
	}
}
