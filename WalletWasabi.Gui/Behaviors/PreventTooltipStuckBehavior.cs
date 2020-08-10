using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Gui.Behaviors
{
	/// <summary>
	/// Ensures Tooltips are closed when parent control is removed from visual tree.
	/// This is a workaround and should be removed when Avalonia 0.10.0 upgrade is complete. TODO
	/// </summary>
	public class PreventTooltipStuckBehavior : Behavior<Control>
	{
		protected override void OnDetaching()
		{
			base.OnDetaching();

			ToolTip.SetIsOpen(AssociatedObject, false);
		}
	}
}