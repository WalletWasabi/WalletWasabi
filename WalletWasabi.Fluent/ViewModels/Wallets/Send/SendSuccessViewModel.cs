using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class SendSuccessViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;
	private readonly SmartTransaction _finalTransaction;

	private SendSuccessViewModel(Wallet wallet, SmartTransaction finalTransaction, string? title = null, string? caption = null)
	{
		_wallet = wallet;
		_finalTransaction = finalTransaction;
		Title = title ?? "Payment successful";

		Caption = caption ?? "Your transaction has been successfully sent.";

		NextCommand = ReactiveCommand.Create(OnNext);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public override string Title { get; protected set; }
	public string? Caption { get; }

	private void OnNext()
	{
		Navigate().Clear();

		var walletViewModel = UiServices.WalletManager.GetWalletViewModel(_wallet);

		walletViewModel.History.SelectTransaction(_finalTransaction.GetHash());
	}
}
