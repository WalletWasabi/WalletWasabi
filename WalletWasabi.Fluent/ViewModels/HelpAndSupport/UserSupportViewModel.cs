using System.IO;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport;

[NavigationMetaData(
	Title = "User Support",
	Caption = "Open Wasabi's user support page",
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
