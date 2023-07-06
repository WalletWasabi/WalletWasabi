using System.Reactive;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Speed Up Transaction")]
public partial class SpeedUpTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	public SpeedUpTransactionDialogViewModel(SmartTransaction speedUpTransaction, SmartTransaction original)
	{
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.Create(
			() =>
			{
				// TODO: Speed up transaction
			});

		FeeDifference = Money.Zero;
	}

	public Money FeeDifference { get; }

	protected override void OnDialogClosed()
	{
	}
}
