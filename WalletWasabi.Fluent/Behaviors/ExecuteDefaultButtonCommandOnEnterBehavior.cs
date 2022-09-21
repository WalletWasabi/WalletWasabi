using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class ExecuteDefaultButtonCommandOnEnterBehavior : AttachedToVisualTreeBehavior<InputElement>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject.OnEvent(InputElement.KeyDownEvent, RoutingStrategies.Tunnel)
			.Where(x => x.EventArgs.Key == Key.Enter)
			.Select(eventPattern => GetDefaultButtons().ToList().Select(b => new { Button = b, eventPattern.EventArgs }))
			.SelectMany(x => x)
			.Do(tuple => TryExecute(tuple.Button, tuple.EventArgs))
			.Subscribe()
			.DisposeWith(disposable);
	}

	private static void TryExecute(ICommandSource button, RoutedEventArgs keyEventArgs)
	{
		if (button.Command is { } command && command.CanExecute(null))
		{
			keyEventArgs.Handled = true;
			command.Execute(null);
		}
	}

	private IEnumerable<Button> GetDefaultButtons()
	{
		return AssociatedObject
			.GetVisualDescendants()
			.OfType<Button>()
			.Where(y => y.IsDefault && y.IsVisible && y.IsEnabled);
	}
}
