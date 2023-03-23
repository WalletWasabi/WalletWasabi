using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "User Support",
	Caption = "Open Wasabi's user support website",
	Order = 0,
	Category = "Help & Support",
	Keywords = new[]
	{
		"User", "Support", "Website"
	},
	IconName = "person_support_regular")]
public partial class UserSupportViewModel : TriggerCommandViewModel
{
	public override ICommand TargetCommand =>
		ReactiveCommand.CreateFromTask(
			async () =>
			await IoHelpers.OpenBrowserAsync(AboutViewModel.UserSupportLink));
}
