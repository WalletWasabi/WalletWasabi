using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels;

[NavigationMetaData(Title = "Success")]
public partial class SuccessViewModel : RoutableViewModel
{
	private SuccessViewModel(string successText)
	{
		SuccessText = successText;
		NextCommand = ReactiveCommand.Create(() => Navigate().Clear());

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string SuccessText { get; }
}
