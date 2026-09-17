using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace WalletWasabi.Fluent.Mobile.Tests;

/// <summary>
/// Static visual regression tests compare final states, not an arbitrary wall-clock
/// sample of a transition. This policy applies only to controls in the test window.
/// Production animation, focus, commands, layout and rendered content are unchanged.
/// </summary>
internal static class MobileSnapshotState
{
	public static void Prepare(Window window)
	{
		foreach (var visual in window.GetVisualDescendants().Prepend(window))
		{
			// Removing transitions disposes their animation bindings and exposes the
			// current base values. A paused clock would instead freeze a random tween.
			visual.Transitions = null;
			PrepareTransform(visual.RenderTransform);
			// Fluent's chevron uses Style.Animations (not Transitions). The animation
			// owns its original RotateTransform, so use a fresh transform at its
			// declared final angle. Keep every pixel and the actual expanded state.
			if (visual is ShapePath { Name: "ExpandCollapseChevron" } &&
				visual.GetVisualAncestors().OfType<Expander>().FirstOrDefault() is { } expander)
				visual.RenderTransform = new RotateTransform(expander.IsExpanded ? 180 : 0);
			if (visual is TextBox textBox)
			{
				// Preserve keyboard focus and its visible focus outline. The blinking
				// insertion caret is excluded from static raster comparisons only.
				textBox.CaretBrush = Brushes.Transparent;
			}
		}
	}

	private static void PrepareTransform(ITransform? transform)
	{
		if (transform is Animatable animatable) animatable.Transitions = null;
		if (transform is TransformGroup group)
			foreach (var child in group.Children) PrepareTransform(child);
	}
}
