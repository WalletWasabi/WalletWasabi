using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors
{
	public class RandomizedOpacityBehavior : Behavior<Control>
	{
		private static readonly List<Control> _targetControls = new();
		private static readonly Random _randomSource = new();
		private static CancellationTokenSource _cts = new();

		private static void RunAnimation()
		{
			Task.Run(() =>
			{
				var targetIndices = new List<int>();

				while (!_cts.IsCancellationRequested)
				{
					if (targetIndices.Count == 0)
					{
						targetIndices.AddRange(Enumerable.Range(0, _targetControls.Count));
					}

					Task.WaitAll(
						targetIndices.OrderBy(_ => _randomSource.Next(0, targetIndices.Count))
										  .Take(4)
										  .Where(x => targetIndices.Remove(x))
										  .ToArray()
										  .Select(x => Animate(_targetControls[x]))
										  .ToArray());
				}
			});
		}

		private static async Task Animate(Control target)
		{
			await Task.Delay(TimeSpan.FromSeconds(1));

			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				target.SetValue(Control.OpacityProperty, 1, BindingPriority.Style);
			});

			await Task.Delay(TimeSpan.FromSeconds(2));

			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				target.SetValue(Control.OpacityProperty, 0, BindingPriority.Style);
			});
		}

		protected override void OnDetaching()
		{
			_targetControls.Remove(AssociatedObject);

			if (_targetControls.Count == 0)
			{
				_cts.Dispose();
			}

			base.OnDetaching();
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			if (AssociatedObject is null)
			{
				return;
			}

			var wasEmpty = _targetControls.Count == 0;

			_targetControls.Add(AssociatedObject);

			Dispatcher.UIThread.InvokeAsync(() =>
			{
				AssociatedObject.SetValue(Control.OpacityProperty, 0, BindingPriority.Style);
			});

			if (!wasEmpty)
			{
				return;
			}

			_cts = new CancellationTokenSource();
			RunAnimation();
		}
	}
}