using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Behaviors;

public class RandomizedWorldPointsBehavior : Behavior<Canvas>
{
	private static readonly Random RandomSource = new();
	private CancellationTokenSource _cts = new();
	private List<Control> _targetControls = new();

	// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
	private static readonly List<Point> WorldLocations = new()
	{
		new(816, 219),
		new(855, 146),
		new(816, 142),
		new(812, 188),
		new(780, 130),
		new(784, 194),
		new(760, 258),
		new(766, 286),
		new(963, 406),
		new(910, 365),
		new(898, 392),
		new(869, 409),
		new(791, 387),
		new(677, 166),
		new(677, 240),
		new(652, 194),
		new(631, 118),
		new(586, 182),
		new(566, 263),
		new(574, 340),
		new(531, 306),
		new(537, 154),
		new(543, 72),
		new(509, 72),
		new(472, 60),
		new(491, 106),
		new(491, 160),
		new(491, 392),
		new(466, 96),
		new(438, 246),
		new(438, 108),
		new(426, 84),
		new(400, 135),
		new(406, 154),
		new(377, 225),
		new(281, 351),
		new(251, 275),
		new(245, 391),
		new(205, 409),
		new(205, 123),
		new(205, 237),
		new(193, 204),
		new(179, 143),
		new(173, 123),
		new(173, 244),
		new(173, 306),
		new(167, 177),
		new(161, 95),
		new(142, 222),
		new(120, 76),
		new(120, 163),
		new(118, 210),
		new(76, 101),
		new(57, 143)
	};

	private void RunAnimation(CancellationToken cancellationToken)
	{
		Task.Run(
			() =>
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						var locations = WorldLocations
							.OrderBy(_ => RandomSource.NextDouble())
							.Take(_targetControls.Count);

						var cities = _targetControls.Zip(locations, (control, point) => (control, point));

						Task.WaitAll(
							cities.Select(x => AnimateCityMarkerAsync(x.control, x.point, cancellationToken)).ToArray(),
							cancellationToken);
					}
					catch (Exception ex)
					{
						if (ex is OperationCanceledException)
						{
							return;
						}

						Logger.LogWarning(
								$"There was a problem while animating in {nameof(RandomizedWorldPointsBehavior)}: '{ex}'.");
					}
				}
			},
			cancellationToken);
	}

	private async Task AnimateCityMarkerAsync(Control target, Point point, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}

		await Dispatcher.UIThread.InvokeAsync(() => target.SetValue(Visual.OpacityProperty, 0, BindingPriority.StyleTrigger));

		await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			target.SetValue(Canvas.LeftProperty, point.X, BindingPriority.StyleTrigger);
			target.SetValue(Canvas.TopProperty, point.Y, BindingPriority.StyleTrigger);
			target.SetValue(Visual.OpacityProperty, 1, BindingPriority.StyleTrigger);
		});

		await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

		await Dispatcher.UIThread.InvokeAsync(() => target.SetValue(Visual.OpacityProperty, 0, BindingPriority.StyleTrigger));
	}

	protected override void OnDetaching()
	{
		_cts.Cancel();
		_cts.Dispose();
		base.OnDetaching();
	}

	protected override void OnAttached()
	{
		base.OnAttached();

		Dispatcher.UIThread.Post(
			() =>
			{
				if (AssociatedObject?.Children is null || AssociatedObject.Children.Count == 0)
				{
					return;
				}

				var targets = AssociatedObject.Children
					.Where(x => x.Classes.Contains("City"))
					.ToList();

				if (targets.Count <= 0)
				{
					return;
				}

				_targetControls = targets.Cast<Control>().ToList();
				_cts?.Dispose();
				_cts = new CancellationTokenSource();

				RunAnimation(_cts.Token);
			},
			DispatcherPriority.Loaded);
	}
}
