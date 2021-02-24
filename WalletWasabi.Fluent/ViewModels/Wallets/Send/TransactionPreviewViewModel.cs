using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		private readonly BuildTransactionResult _transaction;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, BuildTransactionResult transaction)
		{
			_transaction = transaction;

			var destinationAmount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			var fee = transaction.Fee;

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

			PercentFeeText = $"{transaction.FeePercentOfSent:F2}%";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var dialogResult =
					await NavigateDialog(new EnterPasswordDialogViewModel(""), NavigationTarget.DialogScreen);

				if (dialogResult.Kind == DialogResultKind.Normal)
				{
					IsBusy = true;

					string? compatibilityPasswordUsed;

					var passwordValid = await Task.Run(() => PasswordHelper.TryPassword(wallet.KeyManager,
						dialogResult.Result,
						out compatibilityPasswordUsed));

					IsBusy = false;

					if (passwordValid)
					{
						// Broadcast transaction.
					}
					else
					{
						await ShowErrorAsync("Password was incorrect.", "Please try again.");
					}
				}
			});
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