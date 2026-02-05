using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Services;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.Models.UI;

public interface ITorStatusCheckerModel
{
	IObservable<IList<Issue>> Issues { get; }
}

public partial class TorStatusCheckerModel : ITorStatusCheckerModel
{
	public TorStatusCheckerModel()
	{
		Issues =
			Services.EventBus.AsObservable<TorNetworkStatusChanged>()
				.Select(e => e.ReportedIssues.ToList());
	}

	public IObservable<IList<Issue>> Issues { get; }
}
