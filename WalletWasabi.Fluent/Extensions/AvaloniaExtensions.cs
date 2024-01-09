using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WalletWasabi.Fluent.Extensions;

public static class AvaloniaExtensions
{
	public static IObservable<EventPattern<TEventArgs>> OnEvent<TEventArgs>(
		this Interactive target,
		RoutedEvent<TEventArgs> routedEvent,
		RoutingStrategies routingStrategies = RoutingStrategies.Bubble) where TEventArgs : RoutedEventArgs
	{
		return Observable.FromEventPattern<TEventArgs>(
			add => target.AddHandler(routedEvent, add, routingStrategies),
			remove => target.RemoveHandler(routedEvent, remove));
	}

	public static void BringToFront(this Window? window)
	{
		if (window is null)
		{
			return;
		}

		if (OperatingSystem.IsMacOS())
		{
			if (window.IsVisible)
			{
				window.Hide();
			}

			window.Show();
		}

		if (OperatingSystem.IsLinux())
		{
			if (!window.IsActive)
			{
				var position = window.Position;
				if (window.IsVisible)
				{
					window.Hide();
				}

				window.Position = position;
				window.Show();
			}
		}

		if (OperatingSystem.IsWindows())
		{
			window.Topmost = !window.Topmost;
			window.Topmost = !window.Topmost;
		}
	}
}
