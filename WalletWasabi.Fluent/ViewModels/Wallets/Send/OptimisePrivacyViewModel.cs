using NBitcoin;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Optimise your privacy")]
	public partial class OptimisePrivacyViewModel : RoutableViewModel
	{
		private BuildTransactionResult _exactTransaction;
		private BuildTransactionResult? _smallerTransaction;
		private BuildTransactionResult _largerTransaction;

		public OptimisePrivacyViewModel(BuildTransactionResult exactTransaction, Wallet wallet, TransactionInfo transactionInfo)
		{
			_exactTransaction = exactTransaction;
			var intent = new PaymentIntent(
					destination: transactionInfo.Address,
					amount: MoneyRequest.CreateAllRemaining(subtractFee: true),
					label: transactionInfo.Labels);

			_largerTransaction = wallet.BuildTransaction(
				wallet.Kitchen.SaltSoup(),
				intent,
				FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
				allowUnconfirmed: true,
				exactTransaction.SpentCoins.Select(x => x.OutPoint));

			if (exactTransaction.SpentCoins.Count() == 1)
			{
				// If only one coin, then there's no smaller transaction.
				_smallerTransaction = null;
			}
			else
			{
				_smallerTransaction = wallet.BuildTransaction(
					wallet.Kitchen.SaltSoup(),
					intent,
					FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
					allowUnconfirmed: true,
					exactTransaction
						.SpentCoins
						.OrderBy(x => x.Amount)
						.Skip(1)
						.Select(x => x.OutPoint));
			}
		}
	}
}
