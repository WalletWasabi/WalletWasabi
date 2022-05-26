using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Extensions;

public static class ObservableExtension
{
	public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync) =>
		source
			.Select(x => Observable.FromAsync(() => onNextAsync(x)))
			.Concat()
			.Subscribe();
	
	public static IObservable<Unit> ToSignal<T>(this IObservable<T> source) => source.Select(_ => Unit.Default);
}
