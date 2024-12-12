using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Order = 0,
	Category = SearchCategory.HelpAndSupport,
	Title = "UserSupportViewModel_Title",
	Caption = "UserSupportViewModel_Caption",
	Keywords = "UserSupportViewModel_Keywords",
	IconName = "person_support_regular")]
public partial class UserSupportViewModel : TriggerCommandViewModel
{
	private UserSupportViewModel()
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.UserSupportLink));
	}

	public override ICommand TargetCommand { get; }
}
