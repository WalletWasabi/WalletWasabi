using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.Extensions;

public static class EventBusExtensions
{
	extension(EventBus eventBus)
	{
		public IObservable<TEvent> AsObservable<TEvent>() where TEvent : notnull
		{
			return Observable.Create<TEvent>(observer =>
			{
				var subscription = eventBus.Subscribe<TEvent>(eventItem =>
				{
					observer.OnNext(eventItem);
				});

				return subscription;
			});
		}

		public async Task WaitForEventAsync<TEvent>(Func<bool> check) where TEvent : notnull
		{
			var tcs = new TaskCompletionSource();
			using var _ = eventBus.Subscribe<TEvent>(_ => tcs.TrySetResult());
			if (check())
			{
				tcs.TrySetResult();
			}

			await tcs.Task.ConfigureAwait(true);
		}
	}
}
