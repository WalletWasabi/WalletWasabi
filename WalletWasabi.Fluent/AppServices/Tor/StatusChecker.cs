using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.AppServices.Tor;

public class StatusChecker : IStatusChecker
{
	public StatusChecker(ITorNetwork reporter, TimeSpan checkInterval, IScheduler scheduler)
	{
		var timer = Observable.Timer(checkInterval, scheduler).SelectMany(x => reporter.Issues.ToList());
		var initial = Observable.Defer(() => reporter.Issues.ToList());

		Issues =
			initial.Concat(timer)
				.Retry();
	}

	public IObservable<IList<Issue>> Issues { get; }
}
