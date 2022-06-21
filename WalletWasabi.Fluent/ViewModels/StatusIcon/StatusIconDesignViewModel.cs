using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using TorStatusChecker;

namespace WalletWasabi.Fluent.ViewModels.StatusIcon;

class StatusIconDesignViewModel : IStatusIconViewModel
{
	public ICollection<Issue> TorIssues => new List<Issue>()
	{
		new()
		{
			Affected = new List<string>()
			{
				"This",
				"That",
			},
			Title = "Every man for himself!",
			Resolved = false,
			Severity = "OMG!",
			Date = DateTimeOffset.Now.Date,
		},
		new Issue()
		{
			Affected = new List<string>()
			{
				"This",
				"That",
			},
			Title = "Another issue",
			Resolved = false,
			Severity = "minor",
			Date = DateTimeOffset.Now.Date,
		}
	};

	public ICommand OpenTorStatusSiteCommand { get; } = ReactiveCommand.Create(() => { });
}