using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "Find a Coordinator",
	Caption = "Open Wasabi's documentation website",
	Order = 2,
	Category = "Help & Support",
	Keywords =
	[
		"Find", "Coordinator", "Docs", "Documentation", "Guide"
	],
	IconName = "book_question_mark_regular")]
public partial class FindCoordinatorViewModel : TriggerCommandViewModel
{
	private FindCoordinatorViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(CoinJoinStateViewModel.FindCoordinatorLink));
	}

	public override ICommand TargetCommand { get; }
}
