using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Payment successful")]
public partial class SendSuccessViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	private readonly SmartTransaction _finalTransaction;

	public SendSuccessViewModel(Wallet wallet, SmartTransaction finalTransaction)
	{
		_wallet = wallet;
		_finalTransaction = finalTransaction;

		NextCommand = ReactiveCommand.Create(OnNext);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private void OnNext()
	{
		Navigate().Clear();

		var walletViewModel = UiServices.WalletManager.GetWalletViewModel(_wallet);

		walletViewModel.History.SelectTransaction(_finalTransaction.GetHash());
	}
}
