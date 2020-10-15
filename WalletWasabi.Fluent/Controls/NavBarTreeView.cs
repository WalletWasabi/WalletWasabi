using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Styling;
using System;

namespace WalletWasabi.Fluent.Controls
{
	public class NavBarTreeView : TreeView, IStyleable
	{
		Type IStyleable.StyleKey => typeof(TreeView);

		protected override IItemContainerGenerator CreateItemContainerGenerator()
		{
			return new TreeItemContainerGenerator<NavBarTreeItem>(
				this,
				TreeViewItem.HeaderProperty,
				TreeViewItem.ItemTemplateProperty,
				TreeViewItem.ItemsProperty,
				TreeViewItem.IsExpandedProperty);
		}
	}
}
