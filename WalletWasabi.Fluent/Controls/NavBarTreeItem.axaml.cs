using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;

namespace WalletWasabi.Fluent.Controls;

/// <summary>
/// Container for NavBarTree Items.
/// </summary>
public class NavBarTreeItem : TreeViewItem
{
	public static readonly StyledProperty<IconElement> IconProperty =
		AvaloniaProperty.Register<NavBarTreeItem, IconElement>(nameof(Icon));

	public IconElement Icon
	{
		get => GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	protected override IItemContainerGenerator CreateItemContainerGenerator()
	{
		return new TreeItemContainerGenerator<NavBarTreeItem>(
			this,
			HeaderProperty,
			ItemTemplateProperty,
			ItemsProperty,
			IsExpandedProperty);
	}
}
