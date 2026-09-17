using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

public partial class HistoryViewModel
{
	/// <summary>Lets native mobile history handle the same selection request as the desktop grid.</summary>
	public TransactionSelection SelectionRequests { get; } = new();
}
