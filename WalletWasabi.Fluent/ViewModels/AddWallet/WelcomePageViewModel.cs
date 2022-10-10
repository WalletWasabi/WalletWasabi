using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Welcome")]
public partial class WelcomePageViewModel : DialogViewModelBase<Unit>
{
	private const int NumberOfPages = 5;
	private readonly AddWalletPageViewModel _addWalletPage;

	public WelcomePageViewModel(AddWalletPageViewModel addWalletPage)
	{
		_addWalletPage = addWalletPage;

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		NextCommand = ReactiveCommand.Create(OnNext);
	}

	private void OnNext()
	{
		if (!Services.WalletManager.HasWallet())
		{
			Navigate().To(_addWalletPage);
		}
		else
		{
			Close();
		}
	}
}
