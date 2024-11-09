using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.HelpAndSupport,
	Title = "FindCoordinatorLinkViewModel_Title",
	Caption = "FindCoordinatorLinkViewModel_Caption",
	Keywords = "FindCoordinatorLinkViewModel_Keywords",
	IconName = "book_question_mark_regular")]
public partial class FindCoordinatorLinkViewModel : TriggerCommandViewModel
{
	private FindCoordinatorLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(WalletViewModel.FindCoordinatorLink));
	}

	public override ICommand TargetCommand { get; }
}
