using System;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>Observes command execution; never invokes a wallet command.</summary>
public static class MobileCommandActivity
{
	public static IObservable<bool> Observe(IObservable<bool> pageBusy, ICommand? confirm, ICommand? alternate) =>
		Combine(pageBusy, Activity(confirm), Activity(alternate));

	/// <summary>Follows command replacement by navigation-lifetime view models.</summary>
	public static IObservable<bool> ObserveCommands(IObservable<bool> pageBusy,
		IObservable<ICommand?> confirm, IObservable<ICommand?> alternate)
	{
		ArgumentNullException.ThrowIfNull(confirm);
		ArgumentNullException.ThrowIfNull(alternate);
		return Combine(pageBusy,
			confirm.DistinctUntilChanged().Select(Activity).Switch(),
			alternate.DistinctUntilChanged().Select(Activity).Switch());
	}

	public static IObservable<bool> Combine(IObservable<bool> pageBusy, IObservable<bool> confirmBusy, IObservable<bool> alternateBusy)
	{
		ArgumentNullException.ThrowIfNull(pageBusy);
		ArgumentNullException.ThrowIfNull(confirmBusy);
		ArgumentNullException.ThrowIfNull(alternateBusy);
		return Observable.CombineLatest(pageBusy, confirmBusy, alternateBusy,
			(page, confirm, alternate) => page || confirm || alternate).DistinctUntilChanged();
	}

	// ReactiveCommand publishes its current execution state to a new subscriber.
	// Do not prepend false: subscribing to an already-running command must not unlock the page.
	private static IObservable<bool> Activity(ICommand? command) => command is IReactiveCommand reactive
		? reactive.IsExecuting
		: Observable.Return(false);
}
