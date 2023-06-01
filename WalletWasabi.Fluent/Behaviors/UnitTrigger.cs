using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class SignalTrigger : DisposingTrigger
{
	public static readonly StyledProperty<IObservable<Unit>> SignalProperty = AvaloniaProperty.Register<SignalTrigger, IObservable<Unit>>(nameof(Signal));

	public IObservable<Unit> Signal
	{
		get => GetValue(SignalProperty);
		set => SetValue(SignalProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		this.WhenAnyObservable(x => x.Signal)
			.Do(_ => Interaction.ExecuteActions(this, Actions, null))
			.Subscribe()
			.DisposeWith(disposables);
	}
}
