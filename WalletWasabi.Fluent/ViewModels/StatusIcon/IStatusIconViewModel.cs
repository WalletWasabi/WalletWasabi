using System.Collections.Generic;
using System.Windows.Input;
using WalletWasabi.Fluent.AppServices.Tor;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

public interface IStatusIconViewModel
{
	ICollection<Issue> TorIssues { get; }
	ICommand OpenTorStatusSiteCommand { get; }
}
