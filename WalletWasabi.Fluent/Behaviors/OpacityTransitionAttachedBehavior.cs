using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;

namespace WalletWasabi.Fluent.Behaviors;

public class OpacityTransitionAttachedBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<TimeSpan> DurationProperty =
		AvaloniaProperty.Register<OpacityTransitionAttachedBehavior, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(250));

	public TimeSpan Duration
	{
		get => GetValue(DurationProperty);
		set => SetValue(DurationProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AnimateImplicit(AssociatedObject, Duration);
		return;

		AssociatedObject.GetObservable(Visual.IsVisibleProperty).Subscribe(x =>
		{
			if (x)
			{
				Animate(AssociatedObject, Duration);
			}
		});
	}

	private static void Animate(Control control, TimeSpan duration)
	{
		var compositionVisual = ElementComposition.GetElementVisual(control);
		if (compositionVisual is null)
		{
			return;
		}

		var compositor = compositionVisual.Compositor;

		var animation = compositor.CreateScalarKeyFrameAnimation();
		animation.InsertKeyFrame(0f, 0f);
		animation.InsertKeyFrame(1f, 1f);
		animation.Duration = duration;
		animation.Direction = PlaybackDirection.Normal;
		animation.IterationCount = 1;

		compositionVisual.StartAnimation("Opacity", animation);
	}

	private static void AnimateImplicit(Control control, TimeSpan duration)
	{
		var compositionVisual = ElementComposition.GetElementVisual(control);
		if (compositionVisual is null)
		{
			return;
		}

		var compositor = compositionVisual.Compositor;

		var animation = compositor.CreateScalarKeyFrameAnimation();
		animation.Target = "Opacity";
		animation.InsertExpressionKeyFrame(0f, "this.StartingValue");
		animation.InsertExpressionKeyFrame(1f, "this.FinalValue");
		animation.Duration = duration;
		animation.Direction = PlaybackDirection.Normal;
		animation.IterationCount = 1;

		var implicitAnimation = compositor.CreateImplicitAnimationCollection();
		implicitAnimation["Opacity"] = animation;

		compositionVisual.ImplicitAnimations = implicitAnimation;
		compositionVisual.Opacity = 0f;
	}
}
