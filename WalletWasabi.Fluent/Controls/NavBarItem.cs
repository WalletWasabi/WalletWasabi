using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Controls
{
	public class NavBarItem : ContentControl
	{
		/// <summary>
		/// Initializes static members of the <see cref="NavBarItem"/> class.
		/// </summary>
		static NavBarItem ()
		{
			SelectableMixin.Attach<NavBarItem>(IsSelectedProperty);
			PressedMixin.Attach<NavBarItem>();
			FocusableProperty.OverrideDefaultValue<NavBarItem>(true);
		}

		/// <summary>
		/// Defines the <see cref="IsSelected"/> property.
		/// </summary>
		public static readonly StyledProperty<bool> IsSelectedProperty =
			AvaloniaProperty.Register<NavBarItem, bool>(nameof(IsSelected));

		/// <summary>
		/// Gets or sets the selection state of the item.
		/// </summary>
		public bool IsSelected
		{
			get { return GetValue(IsSelectedProperty); }
			set { SetValue(IsSelectedProperty, value); }
		}
	}
}
