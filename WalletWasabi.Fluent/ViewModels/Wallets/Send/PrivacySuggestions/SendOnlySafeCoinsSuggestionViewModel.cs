using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class SendOnlySafeCoinsSuggestionViewModel : ChangeAvoidanceSuggestionViewModel
{
	public SendOnlySafeCoinsSuggestionViewModel(decimal originalAmount, BuildTransactionResult transactionResult, decimal fiatExchangeRate) :
		base(originalAmount, transactionResult, fiatExchangeRate)
	{
	}
}
