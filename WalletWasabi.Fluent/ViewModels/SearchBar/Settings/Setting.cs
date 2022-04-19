using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Settings;

public class Setting<TTarget, TProperty> : ReactiveObject
{
	private TProperty? _value;

	public Setting([DisallowNull] TTarget target, Expression<Func<TTarget, TProperty>> selector,
		bool requiresRestart = false)
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

		ShowRestartNotificationCommand = ReactiveCommand.Create(() =>
		{
			NotificationHelpers.Show(
				new RestartViewModel("To apply the new setting, Wasabi Wallet needs to be restarted"));
		});

		Value = (TProperty?)pr.GetValue(target);

		SetValueCommand = ReactiveCommand.Create(() => pr.SetValue(target, Value));

		this.WhenAnyValue(x => x.Value)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Select(_ => Unit.Default)
			.InvokeCommand(SetValueCommand);

		if (requiresRestart)
		{
			this.WhenAnyValue(r => r.Value)
				.Skip(1)
				.Any()
				.Select(_ => Unit.Default)
				.InvokeCommand(ShowRestartNotificationCommand);
		}
	}

	public ICommand ShowRestartNotificationCommand { get; }

	public TProperty? Value
	{
		get => _value;
		set => this.RaiseAndSetIfChanged(ref _value, value);
	}

	public ReactiveCommand<Unit, Unit> SetValueCommand { get; }
}
