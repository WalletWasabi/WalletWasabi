using System.Numerics;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;

namespace WalletWasabi.Fluent.Behaviors;

public class DialogTransitionAttachedBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<TimeSpan> OpacityDurationProperty =
		AvaloniaProperty.Register<DialogTransitionAttachedBehavior, TimeSpan>(nameof(OpacityDuration), TimeSpan.FromMilliseconds(250));

	public static readonly StyledProperty<TimeSpan> ScaleDurationProperty =
		AvaloniaProperty.Register<DialogTransitionAttachedBehavior, TimeSpan>(nameof(ScaleDuration), TimeSpan.FromMilliseconds(350));

	public static readonly StyledProperty<bool> EnableScaleProperty =
		AvaloniaProperty.Register<DialogTransitionAttachedBehavior, bool>(nameof(EnableScale), true);

	public TimeSpan OpacityDuration
	{
		get => GetValue(OpacityDurationProperty);
		set => SetValue(OpacityDurationProperty, value);
	}

	public TimeSpan ScaleDuration
	{
		get => GetValue(ScaleDurationProperty);
		set => SetValue(ScaleDurationProperty, value);
	}

	public bool EnableScale
	{
		get => GetValue(EnableScaleProperty);
		set => SetValue(EnableScaleProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AnimateImplicit(AssociatedObject, OpacityDuration, EnableScale);
	}

	private static void AnimateImplicit(Control control, TimeSpan duration, bool enableScale)
	{
		var compositionVisual = ElementComposition.GetElementVisual(control);
		if (compositionVisual is null)
		{
			return;
		}

		var compositor = compositionVisual.Compositor;

		var fluentEasing = Easing.Parse("0.4,0,0.6,1");

		var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
		opacityAnimation.Target = "Opacity";
		opacityAnimation.InsertExpressionKeyFrame(0f, "this.StartingValue", fluentEasing);
		opacityAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue", fluentEasing);
		opacityAnimation.Duration = duration;
		opacityAnimation.Direction = PlaybackDirection.Normal;
		opacityAnimation.IterationCount = 1;

		var animationGroup = compositor.CreateAnimationGroup();
		animationGroup.Add(opacityAnimation);

		if (enableScale)
		{
			var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
			scaleAnimation.Target = "Scale";
			scaleAnimation.InsertKeyFrame(0f, new Vector3(0.96f, 0.96f, 0f), fluentEasing);
			scaleAnimation.InsertKeyFrame(1f, new Vector3(0.96f, 0.96f, 0f), fluentEasing);
			scaleAnimation.Duration = TimeSpan.FromMilliseconds(350);
			opacityAnimation.Direction = PlaybackDirection.Normal;
			opacityAnimation.IterationCount = 1;

			animationGroup.Add(scaleAnimation);
		}

		var implicitAnimation = compositor.CreateImplicitAnimationCollection();
		implicitAnimation["Opacity"] = animationGroup;

		compositionVisual.ImplicitAnimations = implicitAnimation;
	}
}
