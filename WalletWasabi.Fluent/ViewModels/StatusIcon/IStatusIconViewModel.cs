using System.Collections.Generic;
using System.Windows.Input;
using WalletWasabi.TorNetworkChecker;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public interface IStatusIconViewModel
{
	ICollection<Issue> TorIssues { get; }
	public ICommand OpenTorStatusSiteCommand { get; }
}
