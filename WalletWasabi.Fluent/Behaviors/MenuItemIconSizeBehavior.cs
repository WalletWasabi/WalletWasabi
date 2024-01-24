using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

// Fix: https://github.com/AvaloniaUI/Avalonia/blob/965d0e973da61d279dad9b382497248de3416e20/src/Avalonia.Themes.Fluent/Controls/MenuItem.xaml#L90
public class MenuItemIconSizeBehavior : Behavior<Viewbox>
{
	protected override void OnAttachedToVisualTree()
	{
		base.OnAttachedToVisualTree();

		if (AssociatedObject is { } viewbox)
		{
			viewbox.Width = 20;
			viewbox.Height = 20;
		}
	}
}
