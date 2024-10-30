using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Order = 1,
	Category = SearchCategory.HelpAndSupport,
	Title = "BugReportLinkViewModel_Title",
	Caption = "BugReportLinkViewModel_Caption",
	Keywords = "BugReportLinkViewModel_Keywords",
	IconName = "bug_regular")]
public partial class BugReportLinkViewModel : TriggerCommandViewModel
{
	private BugReportLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.BugReportLink));
	}

	public override ICommand TargetCommand { get; }
}
