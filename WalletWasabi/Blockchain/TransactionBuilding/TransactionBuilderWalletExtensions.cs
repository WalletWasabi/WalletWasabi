using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public static class TransactionBuilderWalletExtensions
{
	/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
	/// <param name="allowedInputs">Only these inputs allowed to be used to build the transaction. The wallet must know the corresponding private keys.</param>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="ArgumentNullException"></exception>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static BuildTransactionResult BuildTransaction(
		this Wallet wallet,
		string password,
		PaymentIntent payments,
		FeeStrategy feeStrategy,
		bool allowUnconfirmed = false,
		IEnumerable<OutPoint>? allowedInputs = null,
		IPayjoinClient? payjoinClient = null,
		bool allowDoubleSpend = false,
		bool tryToSign = true)
	{
		var builder = new TransactionFactory(wallet.Network, wallet.KeyManager, wallet.Coins, wallet.BitcoinStore.TransactionStore, password, allowUnconfirmed: allowUnconfirmed, allowDoubleSpend: allowDoubleSpend);
		return builder.BuildTransaction(
			payments,
			feeRateFetcher: () =>
			{
				if (feeStrategy.Type == FeeStrategyType.Target)
				{
					return wallet.FeeProvider.AllFeeEstimate?.GetFeeRate(feeStrategy.Target.Value) ?? throw new InvalidOperationException("Cannot get fee estimations.");
				}
				else if (feeStrategy.Type == FeeStrategyType.Rate)
				{
					return feeStrategy.Rate;
				}
				else
				{
					throw new NotSupportedException(feeStrategy.Type.ToString());
				}
			},
			allowedInputs,
			lockTimeSelector: () =>
			{
				var currentTipHeight = wallet.BitcoinStore.SmartHeaderChain.TipHeight;
				return LockTimeSelector.Instance.GetLockTimeBasedOnDistribution(currentTipHeight);
			},
			payjoinClient,
			tryToSign: tryToSign);
	}

	public static BuildTransactionResult BuildChangelessTransaction(
		this Wallet wallet,
		IDestination destination,
		LabelsArray label,
		FeeRate feeRate,
		IEnumerable<SmartCoin> allowedInputs,
		bool allowDoubleSpend = false,
		bool tryToSign = true)
		=> wallet.BuildChangelessTransaction(destination, label, feeRate, allowedInputs.Select(coin => coin.Outpoint), allowDoubleSpend, tryToSign);

	public static BuildTransactionResult BuildChangelessTransaction(
		this Wallet wallet,
		IDestination destination,
		LabelsArray label,
		FeeRate feeRate,
		IEnumerable<OutPoint> allowedInputs,
		bool allowDoubleSpend = false,
		bool tryToSign = true)
	{
		var intent = new PaymentIntent(
			destination,
			MoneyRequest.CreateAllRemaining(subtractFee: true),
			label);

		var txRes = wallet.BuildTransaction(
			wallet.Kitchen.SaltSoup(),
			intent,
			FeeStrategy.CreateFromFeeRate(feeRate),
			allowUnconfirmed: true,
			allowedInputs: allowedInputs,
			allowDoubleSpend: allowDoubleSpend,
			tryToSign: tryToSign);

		return txRes;
	}

	public static BuildTransactionResult BuildTransaction(
		this Wallet wallet,
		IDestination destination,
		Money amount,
		LabelsArray label,
		FeeRate feeRate,
		IEnumerable<SmartCoin> coins,
		bool subtractFee,
		IPayjoinClient? payJoinClient = null,
		bool tryToSign = true)
	{
		if (payJoinClient is { } && subtractFee)
		{
			throw new InvalidOperationException("Not possible to subtract the fee.");
		}

		var intent = new PaymentIntent(
			destination: destination,
			amount: amount,
			subtractFee: subtractFee,
			label: label);

		var txRes = wallet.BuildTransaction(
			password: wallet.Kitchen.SaltSoup(),
			payments: intent,
			feeStrategy: FeeStrategy.CreateFromFeeRate(feeRate),
			allowUnconfirmed: true,
			allowedInputs: coins.Select(coin => coin.Outpoint),
			payjoinClient: payJoinClient,
			tryToSign: tryToSign);

		return txRes;
	}

	public static BuildTransactionResult BuildTransactionForSIB(
		this Wallet wallet,
		IDestination destination,
		Money amount,
		LabelsArray label,
		bool subtractFee,
		IPayjoinClient? payJoinClient = null,
		bool tryToSign = true)
	{
		if (payJoinClient is { } && subtractFee)
		{
			throw new InvalidOperationException("Not possible to subtract the fee.");
		}

		var intent = new PaymentIntent(
			destination: destination,
			amount: amount,
			subtractFee: subtractFee,
			label: label);

		var txRes = wallet.BuildTransaction(
			password: wallet.Kitchen.SaltSoup(),
			payments: intent,
			feeStrategy: FeeStrategy.CreateFromConfirmationTarget(2),
			allowUnconfirmed: true,
			allowedInputs: null,
			payjoinClient: payJoinClient,
			tryToSign: tryToSign);

		return txRes;
	}
}
