using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Models.UI;

public class TorStatusCheckerModel
{
	public TorStatusCheckerModel(TorStatusChecker statusChecker)
	{
		Issues = Observable
			.FromEventPattern<Issue[]>(statusChecker, nameof(TorStatusChecker.StatusEvent))
			.Select(pattern => pattern.EventArgs.ToList());
	}

	public IObservable<IList<Issue>> Issues { get; }
}
