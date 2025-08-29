using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
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
		bool tryToSign = true,
		bool overrideFeeOverpaymentProtection = false)
	{
		FeeRate? feeRate;

		if (feeStrategy.TryGetTarget(out int? target))
		{
			feeRate = wallet.FeeRateEstimations.GetFeeRate(target.Value)
				?? throw new InvalidOperationException("Cannot get fee estimations.");
		}
		else if (!feeStrategy.TryGetFeeRate(out feeRate))
		{
			throw new NotSupportedException(feeStrategy.Type.ToString());
		}

		TransactionParameters parameters = new (
			payments,
			FeeRate: feeRate,
			AllowUnconfirmed: allowUnconfirmed,
			AllowDoubleSpend: allowDoubleSpend,
			AllowedInputs: allowedInputs,
			TryToSign: tryToSign,
			OverrideFeeOverpaymentProtection: overrideFeeOverpaymentProtection);

		var factory = new TransactionFactory(wallet.Network, wallet.KeyManager, wallet.Coins, wallet.BitcoinStore.TransactionStore, password);
		return factory.BuildTransaction(
			parameters,
			lockTimeSelector: () =>
			{
				var currentTipHeight = wallet.BitcoinStore.SmartHeaderChain.TipHeight;
				return LockTimeSelector.Instance.GetLockTimeBasedOnDistribution(currentTipHeight);
			},
			payjoinClient);
	}

	public static BuildTransactionResult BuildChangelessTransaction(
		this Wallet wallet,
		Destination destination,
		LabelsArray label,
		FeeRate feeRate,
		IEnumerable<SmartCoin> allowedInputs,
		bool allowDoubleSpend = false,
		bool tryToSign = true)
		=> wallet.BuildChangelessTransaction(destination, label, feeRate, allowedInputs.Select(coin => coin.Outpoint), allowDoubleSpend, tryToSign);

	public static BuildTransactionResult BuildChangelessTransaction(
		this Wallet wallet,
		Destination destination,
		LabelsArray label,
		FeeRate feeRate,
		IEnumerable<OutPoint> allowedInputs,
		bool allowDoubleSpend = false,
		bool tryToSign = true)
	{
		var intent = destination switch
			{
				Destination.Loudly loudly => new PaymentIntent(
					scriptPubKey: loudly.ScriptPubKey,
					amount: MoneyRequest.CreateAllRemaining(subtractFee: true),
					label: label),
				Destination.Silent silent => new PaymentIntent(
					address: silent.Address,
					amount: MoneyRequest.CreateAllRemaining(subtractFee: true),
					label: label),
				_ => throw new InvalidOperationException("Unknown destination type")
			};

		var txRes = wallet.BuildTransaction(
			wallet.Password,
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
		Destination destination,
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
			password: wallet.Password,
			payments: intent,
			feeStrategy: FeeStrategy.CreateFromFeeRate(feeRate),
			allowUnconfirmed: true,
			allowedInputs: coins.Select(coin => coin.Outpoint),
			payjoinClient: payJoinClient,
			tryToSign: tryToSign);

		return txRes;
	}

	public static BuildTransactionResult BuildTransactionWithoutOverpaymentProtection(
		this Wallet wallet,
		string password,
		PaymentIntent payments,
		FeeStrategy feeStrategy,
		bool allowUnconfirmed = false,
		IEnumerable<OutPoint>? allowedInputs = null,
		IPayjoinClient? payjoinClient = null,
		bool allowDoubleSpend = false)
		=> BuildTransaction(
			wallet,
			password,
			payments,
			feeStrategy,
			allowUnconfirmed,
			allowedInputs,
			payjoinClient,
			allowDoubleSpend,
			tryToSign: true,
			overrideFeeOverpaymentProtection: true);

	public static BuildTransactionResult BuildTransactionForSIB(
		this Wallet wallet,
		Destination destination,
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
			password: wallet.Password,
			payments: intent,
			feeStrategy: FeeStrategy.CreateFromConfirmationTarget(2),
			allowUnconfirmed: true,
			allowedInputs: null,
			payjoinClient: payJoinClient,
			tryToSign: tryToSign);

		return txRes;
	}
}
