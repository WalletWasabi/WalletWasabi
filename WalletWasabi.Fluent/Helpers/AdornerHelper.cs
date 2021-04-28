using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Helpers
{
	public class AdornerHelper
	{
		public static void AddAdorner(Visual visual, Control adorner)
		{
			var layer = AdornerLayer.GetAdornerLayer(visual);

			if (layer is { } && !layer.Children.Contains(adorner))
			{
				AdornerLayer.SetAdornedElement(adorner, visual);

				((ISetLogicalParent) adorner).SetParent(visual);

				layer.Children.Add(adorner);
			}
		}

		public static void RemoveAdorner(Visual visual, Control adorner)
		{
			var layer = AdornerLayer.GetAdornerLayer(visual);

			if (layer is { } && layer.Children.Contains(adorner))
			{
				layer.Children.Remove(adorner);
				((ISetLogicalParent) adorner).SetParent(null);
			}
		}
	}
}