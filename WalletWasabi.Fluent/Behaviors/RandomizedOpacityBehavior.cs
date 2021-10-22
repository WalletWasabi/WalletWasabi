using System;
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
		private static readonly List<Control> TargetControls = new();
		private static readonly Random RandomSource = new();
		private static CancellationTokenSource Cts = new();

		private static void RunAnimation()
		{
			Task.Run(() =>
			{
				var targetIndices = new List<int>();

				while (!Cts.IsCancellationRequested)
				{
					if (targetIndices.Count == 0)
					{
						targetIndices.AddRange(Enumerable.Range(0, TargetControls.Count));
					}

					Task.WaitAll(
						targetIndices.OrderBy(_ => RandomSource.NextDouble())
										  .Take(4)
										  .Where(x => targetIndices.Remove(x))
										  .ToArray()
										  .Select(x => Animate(TargetControls[x]))
										  .ToArray(), Cts.Token);
				}

				Cts.Dispose();
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
			TargetControls.Remove(AssociatedObject);

			if (TargetControls.Count == 0)
			{
				Cts.Cancel();
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

			var wasEmpty = TargetControls.Count == 0;

			TargetControls.Add(AssociatedObject);

			Dispatcher.UIThread.InvokeAsync(() =>
			{
				AssociatedObject.SetValue(Control.OpacityProperty, 0, BindingPriority.Style);
			});

			if (!wasEmpty)
			{
				return;
			}

			Cts = new CancellationTokenSource();
			RunAnimation();
		}
	}
}