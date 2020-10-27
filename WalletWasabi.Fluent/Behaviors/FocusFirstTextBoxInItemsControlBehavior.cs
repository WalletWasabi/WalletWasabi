using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactivity;
using System;

namespace WalletWasabi.Fluent.Behaviors
{
	internal class FocusFirstTextBoxInItemsControlBehavior : Behavior<ItemsControl>
	{
		protected override void OnAttached()
		{
			base.OnAttached();

			if (AssociatedObject is { } ao)
			{
				ao.LayoutUpdated += OnLayoutUpdated;
			}
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			if (AssociatedObject is { } ao)
			{
				ao.LayoutUpdated -= OnLayoutUpdated;
			}
		}

		private void OnLayoutUpdated(object? sender, EventArgs e)
		{
			AssociatedObject.FindLogicalDescendantOfType<TextBox>().Focus();
		}
	}
}