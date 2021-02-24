using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
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
				labels = info.Labels.First();
			}
			else if (info.Labels.Count() > 1)
			{
				labels = string.Join(", ", info.Labels.Take(info.Labels.Count() - 1));

				labels += $" and {info.Labels.Last()}";
			}

			Instruction = $"A total of {destinationAmount} bitcoins (≈{(destinationAmount * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()} USD) will be sent to {labels} via {info.Address.ToString()}";

			Execution =
				$"Bitcoin miners will work hard to confirm your transaction within ~20 minutes for an additional fee of {fee.ToDecimal(MoneyUnit.Satoshi)} satoshis (≈{(fee.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()}) this is a charge equivalent to {transaction.FeePercentOfSent:P}";
		}

		public string Instruction { get; }

		public string Execution { get; }
	}
}