using System.Reactive.Linq;

namespace WalletWasabi.Fluent.Extensions;

public static class ObservableMixin
{
	public static IObservable<bool> DelayTrue(this IObservable<bool> obs, TimeSpan ts)
	{
		return obs
			.Select(isTrue => isTrue ? Observable.Return(true).Delay(ts) : Observable.Return(false))
			.Switch();
	}
}
