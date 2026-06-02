using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "Find a Coordinator",
	Caption = "Open Wasabi's documentation website",
	Order = 3,
	Category = "Help & Support",
	Keywords =
	[
		"Find", "Coordinator", "Coinjoin", "Docs", "Documentation", "Guide"
	],
	IconName = "book_question_mark_regular")]
public partial class FindCoordinatorLinkViewModel : TriggerCommandViewModel
{
	public FindCoordinatorLinkViewModel(UiContext uiContext) : base(uiContext)
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await IoHelpers.OpenBrowserAsync(WalletViewModel.FindCoordinatorLink));
	}

	public override ICommand TargetCommand { get; }
}
