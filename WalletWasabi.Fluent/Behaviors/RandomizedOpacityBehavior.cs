using System;
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
		private static readonly Random _randomSource = new();
		private readonly CancellationTokenSource _cts = new();

		protected override void OnDetaching()
		{
			base.OnDetaching();
			_cts.Dispose();
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			if (AssociatedObject is not null)
			{
				Task.Run(async () =>
				{
					while (!_cts.IsCancellationRequested)
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							AssociatedObject.SetValue(Control.OpacityProperty, 0, BindingPriority.Style);
						});

						await Task.Delay(TimeSpan.FromSeconds(_randomSource.Next(1,10)));

						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							AssociatedObject.SetValue(Control.OpacityProperty, 1, BindingPriority.Style);
						});

						await Task.Delay(TimeSpan.FromSeconds(_randomSource.Next(1,3)));
					}
				});
			}
		}
	}
}