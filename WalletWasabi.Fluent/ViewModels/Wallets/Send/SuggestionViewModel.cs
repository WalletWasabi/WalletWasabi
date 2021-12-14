using System.Windows.Input;
using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class SuggestionViewModel : ViewModelBase
{
	public SuggestionViewModel(BuildTransactionResult? transactionResult, bool isOriginal = false)
	{
		IsOriginal = isOriginal;
		TransactionResult = transactionResult;
	}

	public bool IsOriginal { get; protected set; }

	public BuildTransactionResult? TransactionResult { get; protected set; }
}