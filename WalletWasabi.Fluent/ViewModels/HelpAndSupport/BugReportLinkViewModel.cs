using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Order = 1,
	Category = SearchCategory.HelpAndSupport,
	IconName = "bug_regular",
	IsLocalized = true)]
public partial class BugReportLinkViewModel : TriggerCommandViewModel
{
	private BugReportLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.BugReportLink));
	}

	public override ICommand TargetCommand { get; }
}
