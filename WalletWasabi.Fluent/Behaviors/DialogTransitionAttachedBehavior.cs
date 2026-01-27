using System.Numerics;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Xaml.Interactions.Custom;

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

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		var disposables = new CompositeDisposable();

		AnimateImplicit(AssociatedObject, OpacityDuration, EnableScale, ScaleDuration, disposables);

		return disposables;
	}

	private static void AnimateImplicit(Control control, TimeSpan opacityDuration, bool enableScale, TimeSpan scaleDuration, CompositeDisposable disposables)
	{
		var compositionVisual = ElementComposition.GetElementVisual(control);
		if (compositionVisual is null || compositionVisual.ImplicitAnimations is not null)
		{
			return;
		}

		var compositor = compositionVisual.Compositor;

		var fluentEasing = Easing.Parse("0.4,0,0.6,1");

		var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
		opacityAnimation.Target = "Opacity";
		opacityAnimation.InsertExpressionKeyFrame(0f, "this.StartingValue", fluentEasing);
		opacityAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue", fluentEasing);
		opacityAnimation.Duration = opacityDuration;
		opacityAnimation.Direction = PlaybackDirection.Normal;

		var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
		scaleAnimation.Target = "Scale";
		if (enableScale)
		{
			scaleAnimation.InsertExpressionKeyFrame(0f, "Vector3(0.96+(1.0-0.96)*this.Target.Opacity, 0.96+(1.0-0.96)*this.Target.Opacity, 0)", fluentEasing);
			scaleAnimation.InsertExpressionKeyFrame(1f, "Vector3(0.96+(1.0-0.96)*this.Target.Opacity, 0.96+(1.0-0.96)*this.Target.Opacity, 0)", fluentEasing);
		}
		else
		{
			// Required to make implicit animation for Opacity run on first time.
			scaleAnimation.InsertKeyFrame(0f, new Vector3(1, 1, 0));
			scaleAnimation.InsertKeyFrame(1f, new Vector3(1, 1, 0));
		}
		scaleAnimation.Duration = scaleDuration;
		scaleAnimation.Direction = PlaybackDirection.Normal;

		compositionVisual.CenterPoint = new Vector3((float)control.Bounds.Width / 2, (float)control.Bounds.Height / 2, 0);

		control.GetObservable(Visual.BoundsProperty)
			.Subscribe(_ => compositionVisual.CenterPoint = new Vector3((float)control.Bounds.Width / 2, (float)control.Bounds.Height / 2, 0))
			.DisposeWith(disposables);

		var implicitAnimation = compositor.CreateImplicitAnimationCollection();
		implicitAnimation["Opacity"] = opacityAnimation;
		implicitAnimation["Scale"] = scaleAnimation;

		compositionVisual.ImplicitAnimations = implicitAnimation;
	}
}
