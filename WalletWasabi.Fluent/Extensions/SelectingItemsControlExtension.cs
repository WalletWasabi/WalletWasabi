using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Extensions;

public class SelectingItemsControlExtension
{
	public static readonly AttachedProperty<bool> EnableSelectionAnimationProperty =
		AvaloniaProperty.RegisterAttached<ItemsControl, bool>("EnableSelectionAnimation",
			typeof(SelectingItemsControlExtension));

	static SelectingItemsControlExtension()
	{
		EnableSelectionAnimationProperty.Changed.AddClassHandler<Control>(OnEnableSelectionAnimation);
	}

	private static void OnEnableSelectionAnimation(Control control, AvaloniaPropertyChangedEventArgs args)
	{
		if (control is not ItemsControl listBox)
		{
			return;
		}

		if (args.NewValue is true)
		{
			listBox.PropertyChanged += SelectingItemsControlPropertyChanged;
		}
		else
		{
			listBox.PropertyChanged -= SelectingItemsControlPropertyChanged;
		}
	}

	private static void SelectingItemsControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
	{
		if (sender is not SelectingItemsControl selectingItemsControl ||
		    args.Property != SelectingItemsControl.SelectedIndexProperty ||
		    args.OldValue is not int oldIndex || args.NewValue is not int newIndex)
		{
			return;
		}

		var newSelection = selectingItemsControl.ItemContainerGenerator
			.ContainerFromIndex(newIndex) as ContentControl;
		var oldSelection = selectingItemsControl.ItemContainerGenerator
			.ContainerFromIndex(oldIndex) as ContentControl;

		if (newSelection is not { } || oldSelection is not { })
		{
			var target = newSelection ?? oldSelection;

			if (target is not { } ||
			    target.GetVisualDescendants().Cast<Control>().FirstOrDefault(s => s.Name == "PART_SelectionIndicator") is not Visual
				    targetInd)
			{
				return;
			}

			PlaySingleSelectedIndicator(targetInd);

			return;
		}

		StartOffsetAnimation(newSelection, oldSelection);
	}

	private static void StartOffsetAnimation(TemplatedControl nextSelection, TemplatedControl prevSelection)
	{
		// Find the indicator border
		if (prevSelection.GetTemplateChildren().FirstOrDefault(s => s.Name == "PART_SelectionIndicator") is not Visual
			    prevInd ||
		    nextSelection.GetTemplateChildren().FirstOrDefault(s => s.Name == "PART_SelectionIndicator") is not Visual
			    nextInd)
		{
			return;
		}

		var tmpPrevPos = (prevInd.GetVisualRoot() as TopLevel)?.TransformToVisual(prevInd)?.Transform(new Point(0, 0));
		var tmpNextPos = (nextInd.GetVisualRoot() as TopLevel)?.TransformToVisual(nextInd)?.Transform(new Point(0, 0));
		var tmpDelta = tmpPrevPos - tmpNextPos;

		if (tmpDelta is not { } deltaPos ||
		    deltaPos is { X: > 0, Y: > 0 } ||
		    tmpNextPos is not { } nextPos ||
		    tmpPrevPos is not { } prevPos)
		{
			return;
		}

		var isVertical = deltaPos is { X: 0 };
		var dist = (float)Math.Abs(deltaPos.X + (float)deltaPos.Y);
		var prevSize = prevInd.Bounds.Size;
		var nextSize = nextInd.Bounds.Size;

		var dir = (isVertical ? nextPos.Y : nextPos.X) > (isVertical ? prevPos.Y : prevPos.X);

		ResetIndicator(prevInd);

		PlayIndicatorAnimations(nextInd, isVertical, dir, dist, prevSize, nextSize);
	}

	private static void ResetIndicator(Visual? indicator, float opacity = 0)
	{
		if (indicator == null)
		{
			return;
		}

		var visual = ElementComposition.GetElementVisual(indicator);
		if (visual == null)
		{
			return;
		}

		visual.Opacity = opacity;
	}


	private static void PlayIndicatorAnimations(Visual? indicator, bool isVertical, bool isForward, float distance,
		Size beginSize, Size endSize)
	{
		if (indicator == null)
		{
			return;
		}

		var visual = ElementComposition.GetElementVisual(indicator);
		if (visual == null)
		{
			return;
		}

		var comp = visual.Compositor;
		var duration = TimeSpan.FromSeconds(0.6);
		var size = indicator.Bounds.Size;
		var dimension = (float)(isVertical ? size.Height : size.Width);

		float beginScale, endScale;

		if (isVertical)
		{
			beginScale = (float)(beginSize.Height / size.Height);
			endScale = (float)(endSize.Height / size.Height);
		}
		else
		{
			beginScale = (float)(beginSize.Width / size.Width);
			endScale = (float)(endSize.Width / size.Width);
		}


		var singleStep = new StepEasing();
		var compositionAnimationGroup = comp.CreateAnimationGroup();


		Vector3 ScalarModifier(Vector3 reference, float scalar = 1f) =>
			isVertical ? reference with { Y = scalar } : reference with { X = scalar };

		var scaleAnim = comp.CreateVector3KeyFrameAnimation();
		var s2 = Math.Abs(distance) / dimension + (isForward ? endScale : beginScale);

		scaleAnim.InsertKeyFrame(0f, ScalarModifier(visual.Scale, s2));
		scaleAnim.InsertKeyFrame(1.0f, ScalarModifier(visual.Scale, endScale), new CircularEaseInOut());
		scaleAnim.Duration = duration;
		scaleAnim.Target = "Scale";

		var centerAnim = comp.CreateVector3KeyFrameAnimation();
		var c1 = isForward ? 0.0f : dimension;
		var c2 = isForward ? dimension : 0.0f;

		centerAnim.InsertKeyFrame(0.0f, ScalarModifier(visual.CenterPoint, c1));
		centerAnim.InsertKeyFrame(1.0f, ScalarModifier(visual.CenterPoint, c2), singleStep);
		centerAnim.Duration = duration;
		centerAnim.Target = "CenterPoint";

		compositionAnimationGroup.Add(scaleAnim);
		compositionAnimationGroup.Add(centerAnim);
		visual.StartAnimationGroup(compositionAnimationGroup);
	}


	private static void PlaySingleSelectedIndicator(Visual? indicator, bool isForward = true)
	{
		if (indicator == null)
		{
			return;
		}

		var visual = ElementComposition.GetElementVisual(indicator);
		if (visual == null)
		{
			return;
		}

		var comp = visual.Compositor;
		var duration = TimeSpan.FromSeconds(0.4);

		var scaleAnim = comp.CreateVector3KeyFrameAnimation();

		scaleAnim.InsertKeyFrame(0f, new Vector3(1, (isForward ? 0 : 1), 1));
		scaleAnim.InsertKeyFrame(1.0f, new Vector3(1, (isForward ? 1 : 0), 1), new
			CircularEaseInOut());
		scaleAnim.Duration = duration;

		visual.StartAnimation("Scale", scaleAnim);
	}

	private class StepEasing : IEasing
	{
		public double Ease(double progress)
		{
			return Math.Abs(progress - 1) < double.Epsilon ? 1d : 0d;
		}
	}

	public static bool GetEnableSelectionAnimation(SelectingItemsControl element)
	{
		return element.GetValue(EnableSelectionAnimationProperty);
	}

	public static void SetEnableSelectionAnimation(SelectingItemsControl element, bool value)
	{
		element.SetValue(EnableSelectionAnimationProperty, value);
	}
}
