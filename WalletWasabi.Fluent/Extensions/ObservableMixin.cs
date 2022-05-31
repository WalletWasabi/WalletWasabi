using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Extensions;

public static class ObservableMixin
{
	public static IObservable<T> DelayWhen<T>(this IObservable<T> observable, Func<T, bool> filter, TimeSpan ts)
	{
		return observable
			.Select(x => filter(x) ? Observable.Return(x).Delay(ts) : Observable.Return(x)).Concat();
	}

	public static IObservable<bool> DelayFalse(this IObservable<bool> observable, TimeSpan ts)
	{
		return DelayWhen(observable, b => !b, ts);
	}
}