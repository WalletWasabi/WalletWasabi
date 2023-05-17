using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class KeyDownTrigger : Trigger
{
	public static readonly StyledProperty<RoutingStrategies> EventRoutingStrategyProperty = AvaloniaProperty.Register<KeyDownTrigger, RoutingStrategies>(nameof(EventRoutingStrategy));

	public static readonly StyledProperty<Key> KeyProperty = AvaloniaProperty.Register<KeyDownTrigger, Key>(nameof(Key));
	private readonly CompositeDisposable _disposables = new();

	public RoutingStrategies EventRoutingStrategy
	{
		get => GetValue(EventRoutingStrategyProperty);
		set => SetValue(EventRoutingStrategyProperty, value);
	}

	public Key Key
	{
		get => GetValue(KeyProperty);
		set => SetValue(KeyProperty, value);
	}

	protected override void OnAttached()
	{
		base.OnAttached();

		if (AssociatedObject is InputElement element)
		{
			this.WhenAnyValue(x => x.EventRoutingStrategy, x => x.Key, (strategy, key) => new { Key = key, RoutingStrategy = strategy })
				.Select(args => element.OnEvent(InputElement.KeyDownEvent, args.RoutingStrategy).Where(x => x.EventArgs.Key == args.Key))
				.Switch()
				.Do(_ => Interaction.ExecuteActions(AssociatedObject, Actions, null))
				.Subscribe()
				.DisposeWith(_disposables);
		}
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();
		_disposables.Dispose();
	}
}
