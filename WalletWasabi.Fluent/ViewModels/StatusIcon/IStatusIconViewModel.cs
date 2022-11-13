using System.Collections.Generic;
using System.Windows.Input;
using WalletWasabi.Tor.StatusChecker;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public interface IStatusIconViewModel
{
	ICollection<Issue> TorIssues { get; }
	ICommand OpenTorStatusSiteCommand { get; }
}
