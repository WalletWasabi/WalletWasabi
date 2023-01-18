using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "Report a Bug",
	Caption = "Open Wasabi's GitHub issues website",
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
		new AsyncRelayCommand(async () => await IoHelpers.OpenBrowserAsync(AboutViewModel.BugReportLink));
}
