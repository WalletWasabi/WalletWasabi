using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class Setting<TTarget, TProperty> : ReactiveObject
{
	private TProperty _value;

	public Setting(TTarget target, Expression<Func<TTarget, TProperty>> selector)
	{
		if (selector == null)
		{
			throw new ArgumentNullException(nameof(selector));
		}

		if (PropertyHelper<TTarget>.GetProperty(selector) is not { } pr)
		{
			throw new InvalidOperationException("The expression {selector} is not a valid property selector");
		}

		Value = (TProperty)pr.GetValue(target);

		SetIsActiveCommand = ReactiveCommand.Create(() => pr.SetValue(target, Value));

		this.WhenAnyValue(x => x.Value)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Select(_ => Unit.Default)
			.InvokeCommand(SetIsActiveCommand);
	}

	public TProperty Value
	{
		get => _value;
		set => this.RaiseAndSetIfChanged(ref _value, value);
	}

	public ReactiveCommand<Unit, Unit> SetIsActiveCommand { get; }
}