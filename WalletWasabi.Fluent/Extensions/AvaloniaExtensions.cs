using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WalletWasabi.Fluent.Extensions;

public static class AvaloniaExtensions
{
	public static IObservable<EventPattern<TEventArgs>> OnEvent<TEventArgs>(
		this IInteractive target,
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

		if (!window.IsActive)
		{
			if (window.IsVisible)
			{
				window.Hide();
			}

			window.Show();
		}

		window.Topmost = true;
		if (window.Topmost)
		{
			window.Topmost = false;
		}
	}
}
