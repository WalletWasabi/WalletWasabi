using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Styling;
using Avalonia;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Controls;

public class NavBarListBox : ListBox, IStyleable
{
	public static readonly StyledProperty<bool> ReSelectSelectedItemProperty =
		AvaloniaProperty.Register<NavBarListBox, bool>(nameof(ReSelectSelectedItem), true);

	public bool ReSelectSelectedItem
	{
		get => GetValue(ReSelectSelectedItemProperty);
		set => SetValue(ReSelectSelectedItemProperty, value);
	}

	Type IStyleable.StyleKey => typeof(ListBox);

	protected override IItemContainerGenerator CreateItemContainerGenerator()
	{
		return new ItemContainerGenerator<NavBarItem>(
			this,
			ContentControl.ContentProperty,
			ContentControl.ContentTemplateProperty);
	}

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		var previousSelectedItem = SelectedItem;

		base.OnPointerPressed(e);

		// Trigger SelectedItem change notification on pointer pressed event when it was already selected.
		// This enables view model to receive change notification on pointer pressed events using SelectedItem observable.
		if (ReSelectSelectedItem)
		{
			var isSameSelectedItem = previousSelectedItem is not null && previousSelectedItem == SelectedItem;
			if (isSameSelectedItem)
			{
				SelectedItem = null;
				SelectedItem = previousSelectedItem;
			}
		}
	}
}
