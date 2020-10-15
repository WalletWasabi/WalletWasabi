using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Styling;
using System;

namespace WalletWasabi.Fluent.Controls
{
	public class NavBarListBox : ListBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(ListBox);

		protected override IItemContainerGenerator CreateItemContainerGenerator()
		{
			return new ItemContainerGenerator<NavBarItem>(
				this,
				ListBoxItem.ContentProperty,
				ListBoxItem.ContentTemplateProperty);
		}
	}
}
