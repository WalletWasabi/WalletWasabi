using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Settings;

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

		ShowNotificationCommand = ReactiveCommand.Create(() => NotificationHelpers.Show(new RestartViewModel("To apply the new setting, Wasabi Wallet needs to be restarted")));

		this.WhenAnyValue(x => x.Value)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Skip(1)
			.Select(_ => Unit.Default)
			.InvokeCommand(SetValueCommand);

		this.WhenAnyValue(x => x.Value)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(SettingsTabViewModelBase.ThrottleTime + 50))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Where(_ => SettingsTabViewModelBase.CheckIfRestartIsNeeded())
			.Select(_ => Unit.Default)
			.InvokeCommand(ShowNotificationCommand);
	}

	public ICommand SetValueCommand { get; }

	public ICommand ShowNotificationCommand { get; }
}
