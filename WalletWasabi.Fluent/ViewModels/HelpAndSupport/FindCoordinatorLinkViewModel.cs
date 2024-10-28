using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.HelpAndSupport,
	IconName = "book_question_mark_regular",
	IsLocalized = true)]
public partial class FindCoordinatorLinkViewModel : TriggerCommandViewModel
{
	private FindCoordinatorLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(WalletViewModel.FindCoordinatorLink));
	}

	public override ICommand TargetCommand { get; }
}
