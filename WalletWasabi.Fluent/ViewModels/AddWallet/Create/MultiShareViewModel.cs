using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Multi-share")]
public partial class MultiShareViewModel : RoutableViewModel
{
	private MultiShareViewModel(WalletCreationOptions.AddNewWallet options)
	{
		var multiShareBackup = options.SelectedWalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Shares);

		// TODO:

		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => OnNext(options));

		CancelCommand = ReactiveCommand.Create(OnCancel);
	}

	// TODO:

	private void OnNext(WalletCreationOptions.AddNewWallet options)
	{
		// TODO:
		Navigate().To().ConfirmMultiShare(options);
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		base.OnNavigatedTo(isInHistory, disposables);
	}
}
