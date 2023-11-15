using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Behaviors;

public class AdornerBehavior : AttachedToVisualTreeBehavior<Control>
{
	public Control? Adorner { get; set; }

	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var layer = AdornerLayer.GetAdornerLayer(AssociatedObject);

		if (layer is null || Adorner is null)
		{
			return;
		}

		Adorner.DataContext = AssociatedObject.TemplatedParent;

		layer.Children.Add(Adorner);

		Observable.FromEventPattern(
				handler => AssociatedObject.LayoutUpdated += handler,
				handler =>
				{
					if (AssociatedObject != null)
					{
						AssociatedObject.LayoutUpdated -= handler;
					}
				})
			.Do(_ => ArrangeAdorner(layer, AssociatedObject))
			.Subscribe()
			.DisposeWith(disposables);

		ArrangeAdorner(layer, AssociatedObject);
	}

	private void ArrangeAdorner(Visual layer, Visual associatedObject)
	{
		var translated = associatedObject.TranslatePoint(associatedObject.Bounds.TopLeft, layer);
		if (!translated.HasValue)
		{
			return;
		}

		var finalRect = associatedObject.Bounds.Translate(translated.Value);
		Canvas.SetLeft(Adorner!, finalRect.Right);
		Canvas.SetTop(Adorner!, finalRect.Y + (finalRect.Height / 2 - Adorner!.Bounds.Height / 2));
	}
}
