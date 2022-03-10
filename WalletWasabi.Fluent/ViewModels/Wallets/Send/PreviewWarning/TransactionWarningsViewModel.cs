using System.Collections.ObjectModel;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.PreviewWarning;

public class TransactionWarningsViewModel
{
	public TransactionWarningsViewModel()
	{
		Warnings = new ObservableCollection<TransactionWarningViewModelBase>();
	}

	public ObservableCollection<TransactionWarningViewModelBase> Warnings { get; }

	public void EvaluateTransaction()
	{

	}
}
