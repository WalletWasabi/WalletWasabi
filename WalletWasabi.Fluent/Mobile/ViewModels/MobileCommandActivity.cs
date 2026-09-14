using System;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>
/// Keeps a native page busy for the entire command lifetime, including the period before
/// an asynchronous view model explicitly sets IsBusy. This does not execute any command.
/// </summary>
public static class MobileCommandActivity
{
	public static IObservable<bool> Observe(IObservable<bool> pageBusy, ICommand? confirm, ICommand? alternate)
	{
		return Combine(pageBusy, Activity(confirm), Activity(alternate));
	}

	public static IObservable<bool> Combine(IObservable<bool> pageBusy, IObservable<bool> confirmBusy, IObservable<bool> alternateBusy)
	{
		ArgumentNullException.ThrowIfNull(pageBusy);
		ArgumentNullException.ThrowIfNull(confirmBusy);
		ArgumentNullException.ThrowIfNull(alternateBusy);
		return Observable.CombineLatest(pageBusy, confirmBusy, alternateBusy,
			(page, confirm, alternate) => page || confirm || alternate).DistinctUntilChanged();
	}

	private static IObservable<bool> Activity(ICommand? command) => command is IReactiveCommand reactive
		? reactive.IsExecuting.StartWith(false)
		: Observable.Return(false);
}
