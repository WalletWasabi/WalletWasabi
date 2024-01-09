using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class SendSuccessViewModel : RoutableViewModel
{
	private readonly SmartTransaction _finalTransaction;

	private SendSuccessViewModel(SmartTransaction finalTransaction, string? title = null, string? caption = null)
	{
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

		// TODO: Remove this
		MainViewModel.Instance.NavBar.SelectedWallet?.WalletViewModel?.SelectTransaction(_finalTransaction.GetHash());
	}
}
