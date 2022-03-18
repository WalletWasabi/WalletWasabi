using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.PreviewWarning;

public class LabelTransactionWarningViewModel : TransactionWarningViewModelBase
{
	public LabelTransactionWarningViewModel(SmartLabel labels)
	{
		Labels = labels.Take(1).ToList();
		FilteredLabels = labels.Skip(1).ToList();
	}

	public List<string> Labels { get; }

	public List<string> FilteredLabels { get; }

	public override string Title => "Transaction is not protected!";

	public override string Message => "could know about it. Do coinjoin to avoid them!";
}
