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
	}
}
