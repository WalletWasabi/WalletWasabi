using System.Windows.Input;

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
	public UserSupportViewModel(UiContext uiContext) : base(uiContext)
	{
		TargetCommand = ReactiveCommand.CreateFromTask(async () => await UiContext.FileSystem.OpenBrowserAsync(AboutViewModel.UserSupportLink));
	}

	public override ICommand TargetCommand { get; }
}
