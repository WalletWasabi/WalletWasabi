using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

internal class FocusFirstTextBoxInItemsControlBehavior : Behavior<ItemsControl>
{
	protected override void OnAttached()
	{
		base.OnAttached();

		AssociatedObject!.LayoutUpdated += OnLayoutUpdated;
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		AssociatedObject!.LayoutUpdated -= OnLayoutUpdated;
	}

	private void OnLayoutUpdated(object? sender, EventArgs e)
	{
		AssociatedObject!.LayoutUpdated -= OnLayoutUpdated;
		AssociatedObject.FindLogicalDescendantOfType<TextBox>()?.Focus();
	}
}
