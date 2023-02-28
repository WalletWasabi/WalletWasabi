using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
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

		Observable
			.FromEventPattern<ItemContainerEventArgs>(AssociatedObject.ItemContainerGenerator, nameof(ItemContainerGenerator.Materialized))
			.Select(x => x.EventArgs)
			.Subscribe(e =>
			{
				foreach (var c in e.Containers)
				{
					if (c.ContainerControl is not Visual v)
					{
						continue;
					}

					var duration = ItemDuration * (c.Index + 1);
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

					Dispatcher.UIThread.InvokeAsync(async () =>
					{
						v.Opacity = 0;
						await animation.RunAsync(v, null);
						v.Opacity = 1;
					});
				}
			})
			.DisposeWith(disposable);
	}
}
