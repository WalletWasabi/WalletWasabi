using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class NavBarTreeView : TreeView, IStyleable
{
	Type IStyleable.StyleKey => typeof(TreeView);

	protected override IItemContainerGenerator CreateItemContainerGenerator()
	{
		return new TreeItemContainerGenerator<NavBarTreeItem>(
			this,
			HeaderedItemsControl.HeaderProperty,
			ItemTemplateProperty,
			ItemsProperty,
			TreeViewItem.IsExpandedProperty);
	}
}
