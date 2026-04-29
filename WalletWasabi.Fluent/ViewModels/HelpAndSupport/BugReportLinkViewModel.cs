using System.Windows.Input;

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
	public BugReportLinkViewModel(UiContext uiContext) : base(uiContext)
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.BugReportLink));
	}

	public override ICommand TargetCommand { get; }
}
