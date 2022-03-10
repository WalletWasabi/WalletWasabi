using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.PreviewWarning;

public class LabelTransactionWarningViewModel : TransactionWarningViewModelBase
{
	public LabelTransactionWarningViewModel(string title, string message, SmartLabel labels) : base(title, message)
	{
		Labels = labels;
	}

	public SmartLabel Labels { get; }
}
