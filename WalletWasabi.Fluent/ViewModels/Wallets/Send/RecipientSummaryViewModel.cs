using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class RecipientSummaryViewModel : ViewModelBase
{
	public RecipientSummaryViewModel(UiContext uiContext, string addressText, Amount amount, LabelsArray recipient) : base(uiContext)
	{
		AddressText = addressText;
		Amount = amount;
		Recipient = recipient;
	}

	public string AddressText { get; }

	public Amount Amount { get; }

	public LabelsArray Recipient { get; }
}
