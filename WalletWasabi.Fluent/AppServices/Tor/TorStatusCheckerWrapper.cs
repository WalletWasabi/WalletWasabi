using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.AppServices.Tor;

public class TorStatusCheckerWrapper
{
	public TorStatusCheckerWrapper(TorStatusChecker statusChecker)
	{
		Issues = Observable
			.FromEventPattern<Issue[]>(statusChecker, nameof(TorStatusChecker.StatusEvent))
			.Select(pattern => pattern.EventArgs.ToList());
	}

	public IObservable<IList<Issue>> Issues { get; }
}
