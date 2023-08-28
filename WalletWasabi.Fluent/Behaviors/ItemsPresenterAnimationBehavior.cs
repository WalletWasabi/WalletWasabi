using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Styling;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Behaviors;

public class ItemsControlAnimationBehavior : AttachedToVisualTreeBehavior<ItemsControl>
{
	public static readonly StyledProperty<TimeSpan> InitialDelayProperty =
		AvaloniaProperty.Register<ItemsControlAnimationBehavior, TimeSpan>(nameof(InitialDelay), TimeSpan.FromMilliseconds(500));

	public static readonly StyledProperty<TimeSpan> ItemDurationProperty =
		AvaloniaProperty.Register<ItemsControlAnimationBehavior, TimeSpan>(nameof(ItemDuration), TimeSpan.FromMilliseconds(10));

	public TimeSpan InitialDelay
	{
		get => GetValue(InitialDelayProperty);
		set => SetValue(InitialDelayProperty, value);
	}

	public TimeSpan ItemDuration
	{
		get => GetValue(ItemDurationProperty);
		set => SetValue(ItemDurationProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		// TODO: ItemContainerGenerator was totally refactored, there are no events I think, might need to subclass to get callbacks
		/*
		Observable
			.FromEventPattern<ContainerPreparedEventArgs>(AssociatedObject.ItemContainerGenerator, nameof(ItemsControl.ContainerPrepared))
			.Select(x => x.EventArgs)
			.Subscribe(e =>
			{
				if (e.Container is not Visual v)
				{
					return;
				}

				var duration = ItemDuration * (e.Index + 1);
				var totalDuration = InitialDelay + (duration * 2);

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
							KeyTime = duration + InitialDelay,
							Setters =
							{
								new Setter(Visual.OpacityProperty, 0d),
							}
						},
						new KeyFrame
						{
							KeyTime = totalDuration,
							Setters =
							{
								new Setter(Visual.OpacityProperty, 1d),
							}
						}
					}
				};

				Dispatcher.UIThread.Post(async () =>
				{
					v.Opacity = 0;
					await animation.RunAsync(v);
					v.Opacity = 1;
				});
			})
			.DisposeWith(disposable);
			*/
	}
}
