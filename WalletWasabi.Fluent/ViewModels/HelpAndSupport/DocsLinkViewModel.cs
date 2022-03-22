using System.IO;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "User Guide/Docs",
	Caption = "Opens a website in the default browser",
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
