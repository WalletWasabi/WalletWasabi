using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using SharpDX;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, TransactionBroadcaster broadcaster,
			BuildTransactionResult buildTransactionResult)
		{
			var destinationAmount = buildTransactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			var fee = buildTransactionResult.Fee;

			var labels = "";

			if (info.Labels.Count() == 1)
			{
				labels = info.Labels.First() + " ";
			}
			else if (info.Labels.Count() > 1)
			{
				labels = string.Join(", ", info.Labels.Take(info.Labels.Count() - 1));

				labels += $" and {info.Labels.Last()} ";
			}

			BtcAmountText = $"{destinationAmount} bitcoins ";

			FiatAmountText = $"(≈{(destinationAmount * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()} USD) ";

			LabelsText = labels;

			AddressText = info.Address.ToString();

			ConfirmationTimeText = "~20 minutes ";

			BtcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} satoshis ";

			FiatFeeText =
				$"(≈{(fee.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()} USD)";

			PercentFeeText = $"{buildTransactionResult.FeePercentOfSent:F2}%";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var authDialog = AuthorizationHelpers.GetAuthorizationDialog(wallet, buildTransactionResult);

				var authDialogResult = await NavigateDialog(authDialog, NavigationTarget.DialogScreen);

				if (authDialogResult.Result is { } signedTransaction)
				{
					await broadcaster.SendTransactionAsync(signedTransaction);
					Navigate().Clear();
				}
			});

			EnableAutoBusyOn(NextCommand);
		}

		public string BtcAmountText { get; }

		public string FiatAmountText { get; }

		public string LabelsText { get; }

		public string AddressText { get; }

		public string ConfirmationTimeText { get; }

		public string BtcFeeText { get; }

		public string FiatFeeText { get; }

		public string PercentFeeText { get; }
	}
}
