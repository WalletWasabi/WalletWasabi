using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Gui.Behaviors
{
	public class FocusOnAttachedBehavior : Behavior<Control>
	{
		protected override void OnAttached()
		{
			base.OnAttached();

			Dispatcher.UIThread.Post(() =>
			{
				AssociatedObject.Focus();
			}, DispatcherPriority.Layout);
		}
	}
}
