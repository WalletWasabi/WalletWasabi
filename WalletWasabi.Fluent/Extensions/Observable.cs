using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Extensions;

public class ObservableUtils
{
	public static IObservable<Unit> Do(Action action)
	{
		var observable = Observable.Defer(() => System.Reactive.Linq.Observable.Start(action, RxApp.MainThreadScheduler));
		return observable;
	}

	public static IObservable<Unit> Timer(TimeSpan ts)
	{
		return Observable.Timer(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler).ToSignal();
	}
}
