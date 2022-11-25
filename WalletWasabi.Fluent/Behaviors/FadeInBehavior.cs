using Avalonia;
using Avalonia.Animation;
using Avalonia.Styling;
using System.Reactive.Disposables;
using System.Threading;

namespace WalletWasabi.Fluent.Behaviors;

public class FadeInBehavior : AttachedToVisualTreeBehavior<Visual>
{
	public static readonly StyledProperty<TimeSpan> InitialDelayProperty =
		AvaloniaProperty.Register<ItemsControlAnimationBehavior, TimeSpan>(nameof(InitialDelay), TimeSpan.FromMilliseconds(500));

	public static readonly StyledProperty<TimeSpan> DurationProperty =
		AvaloniaProperty.Register<ItemsControlAnimationBehavior, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(250));

	public TimeSpan InitialDelay
	{
		get => GetValue(InitialDelayProperty);
		set => SetValue(InitialDelayProperty, value);
	}

	public TimeSpan Duration
	{
		get => GetValue(DurationProperty);
		set => SetValue(DurationProperty, value);
	}

	private CancellationTokenSource? _animationCts;

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var totalDuration = InitialDelay + Duration;

		var animation = new Animation
		{
			Duration = totalDuration,
			Children =
			{
				new KeyFrame
				{
					KeyTime = TimeSpan.Zero,
					Setters =
					{
						new Setter(Visual.OpacityProperty, 0d),
					}
				},
				new KeyFrame
				{
					KeyTime = InitialDelay,
					Setters =
					{
						new Setter(Visual.OpacityProperty, 0d),
					}
				},
				new KeyFrame
				{
					KeyTime = Duration,
					Setters =
					{
						new Setter(Visual.OpacityProperty, 1d),
					}
				}
			}
		};

		_animationCts = new CancellationTokenSource();
		animation.RunAsync(AssociatedObject, null, _animationCts.Token);
	}

	protected override void OnDetachedFromVisualTree()
	{
		base.OnDetachedFromVisualTree();

		_animationCts?.Cancel();
		_animationCts?.Dispose();
		_animationCts = null;
	}
}
