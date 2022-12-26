using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "User Guide/Docs",
	Caption = "Open Wasabi's documentation website",
	Order = 2,
	Category = "Help & Support",
	Keywords = new[]
	{
		"User", "Support", "Website", "Docs", "Documentation", "Guide"
	},
	IconName = "book_question_mark_regular")]
public partial class DocsLinkViewModel : TriggerCommandViewModel
{
	public override ICommand TargetCommand =>
		ReactiveCommand.CreateFromTask(async () => await IoHelpers.OpenBrowserAsync(AboutViewModel.DocsLink));
}
