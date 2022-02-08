using System.IO;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "Report a Bug",
	Caption = "Opens a website in the default browser",
	Order = 1,
	Category = "Help & Support",
	Keywords = new[]
	{
			"Support", "Website", "Bug", "Report"
	},
	IconName = "bug_regular")]
public partial class BugReportLinkViewModel : TriggerCommandViewModel
{
	public override ICommand TargetCommand =>
		ReactiveCommand.CreateFromTask(async () => await IoHelpers.OpenBrowserAsync(AboutViewModel.BugReportLink));
}
