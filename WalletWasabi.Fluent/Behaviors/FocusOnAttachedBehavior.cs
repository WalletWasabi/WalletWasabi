using Avalonia.Controls;

namespace WalletWasabi.Fluent.Behaviors
{
	public class FocusOnAttachedBehavior : AttachedToVisualTreeBehavior<Control>
	{
		protected override void OnAttachedToVisualTree()
		{
			AssociatedObject?.Focus();
		}
	}
}