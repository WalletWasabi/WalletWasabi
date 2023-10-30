using Avalonia;
using Avalonia.Animation;
using Avalonia.Styling;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.Behaviors;

public class FadeInBehavior : AttachedToVisualTreeBehavior<Visual>
{
	public static readonly StyledProperty<TimeSpan> InitialDelayProperty =
		AvaloniaProperty.Register<FadeInBehavior, TimeSpan>(nameof(InitialDelay), TimeSpan.FromMilliseconds(500));

	public static readonly StyledProperty<TimeSpan> DurationProperty =
		AvaloniaProperty.Register<FadeInBehavior, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(250));

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
		animation.RunAsync(AssociatedObject);
	}
}
