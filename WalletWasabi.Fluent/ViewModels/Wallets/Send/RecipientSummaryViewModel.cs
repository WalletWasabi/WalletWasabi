using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class RecipientSummaryViewModel : ViewModelBase
{
	public RecipientSummaryViewModel(string addressText, Amount amount, LabelsArray recipient)
	{
		AddressText = addressText;
		Amount = amount;
		Recipient = recipient;
	}

	public string AddressText { get; }

	public Amount Amount { get; }

	public LabelsArray Recipient { get; }
}
