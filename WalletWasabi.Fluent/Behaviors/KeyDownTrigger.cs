using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

public class KeyDownTrigger : DisposingTrigger
{
	public static readonly StyledProperty<RoutingStrategies> EventRoutingStrategyProperty = AvaloniaProperty.Register<KeyDownTrigger, RoutingStrategies>(nameof(EventRoutingStrategy));

	public static readonly StyledProperty<Key> KeyProperty = AvaloniaProperty.Register<KeyDownTrigger, Key>(nameof(Key));

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

	public bool MarkAsHandled { get; set; }

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is InputElement element)
		{
			element
				.AddDisposableHandler(InputElement.KeyDownEvent, OnKeyDown, EventRoutingStrategy)
				.DisposeWith(disposables);
		}
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key)
		{
			e.Handled = MarkAsHandled;
			Interaction.ExecuteActions(AssociatedObject, Actions, null);
		}
	}
}
