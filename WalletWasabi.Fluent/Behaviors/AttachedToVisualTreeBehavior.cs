using Avalonia;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors
{
	public abstract class AttachedToVisualTreeBehavior<T> : Behavior<T> where T : Visual
	{
		protected override void OnAttached()
		{
			base.OnAttached();

			AssociatedObject!.AttachedToVisualTree += AssociatedObjectOnAttachedToVisualTree;
		}

		private void AssociatedObjectOnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
		{
			OnAttachedToVisualTree();
		}

		protected abstract void OnAttachedToVisualTree();

		protected override void OnDetaching()
		{
			base.OnDetaching();

			AssociatedObject!.AttachedToVisualTree -= AssociatedObjectOnAttachedToVisualTree;
		}
	}
}