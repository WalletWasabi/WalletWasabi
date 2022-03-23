using System.Collections.ObjectModel;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Windows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.PreviewWarning;

public class TransactionWarningsViewModel
{
	private readonly int _privacyThreshold;

	public TransactionWarningsViewModel(int privacyThreshold)
	{
		_privacyThreshold = privacyThreshold;
		Warnings = new ObservableCollection<TransactionWarningViewModelBase>();
	}

	public ObservableCollection<TransactionWarningViewModelBase> Warnings { get; }

	public void EvaluateTransaction(BuildTransactionResult transaction, TransactionInfo info)
	{
		Warnings.Clear();

		var labels = SmartLabel.Merge(transaction.SpentCoins.Select(x => x.GetLabels(_privacyThreshold)));
		var exactPocketUsed = labels.Count() == info.UserLabels.Count() && labels.All(label => info.UserLabels.Contains(label, StringComparer.OrdinalIgnoreCase));

		if (labels.Any() && !exactPocketUsed)
		{
			Warnings.Add(new LabelTransactionWarningViewModel(labels));
		}
	}
}
