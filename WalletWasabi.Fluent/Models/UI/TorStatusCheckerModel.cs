using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Models.UI;

public partial class TorStatusCheckerModel
{
	public TorStatusCheckerModel(IServices services)
	{
		Issues = services.EventBus
			.AsObservable<TorNetworkStatusChanged>()
			.Select(e => e.ReportedIssues.ToList());
	}

	public IObservable<IList<Issue>> Issues { get; }
}
