using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class UnitTrigger : DisposingTrigger
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		this.WhenAnyObservable(x => x.Signal)
			.Do(_ => Interaction.ExecuteActions(this, Actions, null))
			.Subscribe(unit => { })
			.DisposeWith(disposables);

		this.WhenAnyValue(x => x.Signal).Subscribe(observable => { });
	}

	public static readonly StyledProperty<IObservable<Unit>> SignalProperty = AvaloniaProperty.Register<UnitTrigger, IObservable<Unit>>("Signal");

	public IObservable<Unit> Signal
	{
		get => GetValue(SignalProperty);
		set => SetValue(SignalProperty, value);
	}
}
