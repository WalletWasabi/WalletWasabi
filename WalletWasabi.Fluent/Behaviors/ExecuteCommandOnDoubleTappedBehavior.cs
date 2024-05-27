using System.Reactive.Disposables;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

public class ExecuteCommandOnDoubleTappedBehavior : ExecuteCommandBaseBehavior
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		Gestures.DoubleTappedEvent.AddClassHandler<InputElement>(
				(x, _) =>
				{
					if (Equals(x, AssociatedObject))
					{
						var parameter = CommandParameter;
						if (Command is { } cmd && cmd.CanExecute(parameter))
						{
							cmd.Execute(parameter);
						}
					}
				},
				RoutingStrategies.Tunnel | RoutingStrategies.Bubble)
			.DisposeWith(disposables);
	}
}
