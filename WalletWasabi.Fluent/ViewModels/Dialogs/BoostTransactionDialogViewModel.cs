using System.Reactive;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Boost transaction")]
public partial class BoostTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	public BoostedTransactionPreview TransactionPreview { get; }

	public BoostTransactionDialogViewModel(BoostedTransactionPreview transactionPreview)
	{
		TransactionPreview = transactionPreview;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
	}

	protected override void OnDialogClosed()
	{
	}
}

public class BoostedTransactionPreview
{
	public string Destination { get; init; }
	public Money Amount { get; init; }
	public LabelsArray Labels { get; init; }
	public Money Fee { get; init; }
	public TimeSpan ConfirmationTime { get; init; }
}
