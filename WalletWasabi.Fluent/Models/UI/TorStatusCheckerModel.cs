using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Models.UI;

[AutoInterface]
public partial class TorStatusCheckerModel
{
	public TorStatusCheckerModel()
	{
		Issues =
			Observable.FromEventPattern<Issue[]>(Services.TorStatusChecker, nameof(TorStatusChecker.StatusEvent))
					  .Select(pattern => pattern.EventArgs.ToList());
	}

	public IObservable<IList<Issue>> Issues { get; }
}
