using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class OnSignalTriggerBehavior : Trigger
{
	public static readonly StyledProperty<IObservable<Unit>> TriggerProperty = AvaloniaProperty.Register<OnSignalTriggerBehavior, IObservable<Unit>>(nameof(Trigger));

	public OnSignalTriggerBehavior()
	{
		this.WhenAnyObservable(x => x.Trigger)
			.Do(_ => Interaction.ExecuteActions(this, Actions, null))
			.Subscribe();
	}

	public IObservable<Unit> Trigger
	{
		get => GetValue(TriggerProperty);
		set => SetValue(TriggerProperty, value);
	}
}
