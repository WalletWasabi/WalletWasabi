using System.Reactive;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
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
		NextCommand = ReactiveCommand.Create(
			() =>
			{
				// TODO: Boost transaction
			});
	}

	protected override void OnDialogClosed()
	{
	}
}

public class BoostedTransactionPreview
{
	private readonly decimal _exchangeRate;

	public BoostedTransactionPreview(decimal exchangeRate)
	{
		_exchangeRate = exchangeRate;
	}

	public string Destination { get; init; }
	public Money Amount { get; init; }
	public LabelsArray Labels { get; init; }
	public Money Fee { get; init; }
	public TimeSpan ConfirmationTime { get; init; }
	public string AmountText => Amount.ToBtcWithUnitAndConversion(_exchangeRate);
	public string FeeRaw => Amount.ToFeeDisplayUnitRawString();
	public string FeeText => Fee.ToFeeWithConversion(_exchangeRate);
}
