using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

public class TransactionDetailsViewModel : ViewModelBase
{
	public TransactionDetailsViewModel(TransactionSummary transactionSummary, Wallet wallet)
	{
		Date = transactionSummary.DateTime.ToLocalTime();
		TransactionId = transactionSummary.TransactionId.ToString();
		Labels = transactionSummary.Label;
		BlockHeight = transactionSummary.Height.Type == HeightType.Chain ? transactionSummary.Height.Value : 0;
		Confirmations = transactionSummary.Height.Type == HeightType.Chain ? (int)wallet.BitcoinStore.SmartHeaderChain.TipHeight - transactionSummary.Height.Value + 1 : 0;
		Amount = transactionSummary.Amount.ToString(fplus: false);
	}

	public SmartLabel Labels { get; }

	public string Amount { get; }

	public bool IsConfirmed => Confirmations > 0;

	public int Confirmations { get; }

	public int BlockHeight { get; }

	public string TransactionId { get; }

	public DateTimeOffset Date { get; }
}