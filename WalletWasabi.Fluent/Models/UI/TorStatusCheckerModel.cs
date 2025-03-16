using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Alias;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Models.UI;

[AutoInterface]
public partial class TorStatusCheckerModel
{
	public TorStatusCheckerModel()
	{
		Issues =
			Services.EventBus.AsObservable<TorNetworkStatusChanged>()
				.Select(e => e.ReportedIssues.ToList());
	}

	public IObservable<IList<Issue>> Issues { get; }
}
