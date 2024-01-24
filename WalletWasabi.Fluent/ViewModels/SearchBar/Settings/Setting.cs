using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public partial class Setting<TTarget, TProperty> : ReactiveObject
{
	[AutoNotify] private TProperty? _value;

	public Setting([DisallowNull] TTarget target, Expression<Func<TTarget, TProperty>> selector)
	{
		if (target == null)
		{
			throw new ArgumentNullException(nameof(target));
		}

		if (selector == null)
		{
			throw new ArgumentNullException(nameof(selector));
		}

		if (PropertyHelper<TTarget>.GetProperty(selector) is not { } pr)
		{
			throw new InvalidOperationException($"The expression {selector} is not a valid property selector");
		}

		Value = (TProperty?)pr.GetValue(target);

		SetValueCommand = ReactiveCommand.Create(() => pr.SetValue(target, Value));

		this.WhenAnyValue(x => x.Value)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Skip(1)
			.ToSignal()
			.InvokeCommand(SetValueCommand);
	}

	public ICommand SetValueCommand { get; }
}
