using WalletWasabi.Blockchain.TransactionBuilding;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class SuggestionViewModel : ViewModelBase
{
	public bool IsOriginal { get; protected set; }

	public string Suggestion { get; set; }

	public BuildTransactionResult TransactionResult { get; protected set; }
}