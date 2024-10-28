using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Order = 2,
	Category = SearchCategory.HelpAndSupport,
	IconName = "book_question_mark_regular",
	IsLocalized = true)]
public partial class DocsLinkViewModel : TriggerCommandViewModel
{
	private DocsLinkViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.DocsLink));
	}

	public override ICommand TargetCommand { get; }
}
