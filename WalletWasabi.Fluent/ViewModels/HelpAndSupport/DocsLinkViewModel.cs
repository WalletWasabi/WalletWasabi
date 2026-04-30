using System.Windows.Input;

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
	public DocsLinkViewModel(UiContext uiContext) : base(uiContext)
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.DocsLink));
	}

	public override ICommand TargetCommand { get; }
}
