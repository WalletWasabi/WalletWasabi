using ReactiveUI;
using System.Collections.Generic;
using System.Windows.Input;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public class StatusIconDesignViewModel : IStatusIconViewModel
{
	public ICollection<Issue> TorIssues => new List<Issue>
	{
		new("Issue 1", false),
		new("Issue 2", false),
		new("Issue 3", true)
	};

	public ICommand OpenTorStatusSiteCommand { get; } = ReactiveCommand.Create(() => { });
}
