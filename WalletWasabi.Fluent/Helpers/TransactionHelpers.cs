using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.Helpers;

public static class TransactionHelpers
{
	public static BuildTransactionResult BuildChangelessTransaction(Wallet wallet, BitcoinAddress address, SmartLabel labels, FeeRate feeRate, IEnumerable<SmartCoin> coins, bool tryToSign = true)
	{
		var intent = new PaymentIntent(
			address,
			MoneyRequest.CreateAllRemaining(subtractFee: true),
			labels);

		var txRes = wallet.BuildTransaction(
			wallet.Kitchen.SaltSoup(),
			intent,
			FeeStrategy.CreateFromFeeRate(feeRate),
			allowUnconfirmed: true,
			allowedInputs: coins.Select(coin => coin.Outpoint),
			tryToSign: tryToSign);

		return txRes;
	}

	public static BuildTransactionResult BuildTransaction(Wallet wallet, BitcoinAddress address, Money amount, SmartLabel labels, FeeRate feeRate, IEnumerable<SmartCoin> coins, bool subtractFee, IPayjoinClient? payJoinClient = null, bool tryToSign = true)
	{
		if (payJoinClient is { } && subtractFee)
		{
			throw new InvalidOperationException("Not possible to subtract the fee.");
		}

		var intent = new PaymentIntent(
			destination: address,
			amount: amount,
			subtractFee: subtractFee,
			label: labels);

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

	public static BuildTransactionResult BuildTransaction(Wallet wallet, TransactionInfo transactionInfo, bool isPayJoin = false, bool tryToSign = true)
	{
		if (transactionInfo.IsOptimized)
		{
			return BuildChangelessTransaction(
				wallet,
				transactionInfo.Destination,
				transactionInfo.Recipient,
				transactionInfo.FeeRate,
				transactionInfo.ChangelessCoins,
				tryToSign: tryToSign);
		}

		if (isPayJoin && transactionInfo.SubtractFee)
		{
			throw new InvalidOperationException("Not possible to subtract the fee.");
		}

		return BuildTransaction(
			wallet,
			transactionInfo.Destination,
			transactionInfo.Amount,
			transactionInfo.Recipient,
			transactionInfo.FeeRate,
			transactionInfo.Coins,
			transactionInfo.SubtractFee,
			isPayJoin ? transactionInfo.PayJoinClient : null,
			tryToSign: tryToSign);
	}

	public static bool TryBuildTransactionWithoutPrevTx(
		KeyManager keyManager,
		TransactionInfo transactionInfo,
		ICoinsView allCoins,
		IEnumerable<SmartCoin> allowedCoins,
		string password,
		out Money minimumAmount)
	{
		minimumAmount = transactionInfo.Amount;

		try
		{
			var intent = new PaymentIntent(
				destination: transactionInfo.Destination,
				amount: transactionInfo.Amount,
				subtractFee: transactionInfo.SubtractFee,
				label: transactionInfo.Recipient);

			var network = keyManager.GetNetwork();
			var builder = new TransactionFactory(network, keyManager, allCoins, new EmptyTransactionStore(network), password, true);

			builder.BuildTransaction(
				intent,
				feeRateFetcher: () => transactionInfo.FeeRate,
				allowedCoins.Select(x => x.Outpoint),
				lockTimeSelector: () => LockTime.Zero, // Doesn't matter.
				transactionInfo.PayJoinClient,
				tryToSign: false);

			return true;
		}
		catch (InsufficientBalanceException ex)
		{
			minimumAmount = ex.Minimum;
		}
		catch (Exception)
		{
			// Ignore.
		}

		return false;
	}

	public static async Task<SmartTransaction> ParseTransactionAsync(string path, Network network)
	{
		var psbtBytes = await File.ReadAllBytesAsync(path);
		PSBT psbt;

		try
		{
			psbt = PSBT.Load(psbtBytes, network);
		}
		catch (FormatException ex)
		{
			throw new FormatException("An error occurred while loading the PSBT file.", ex);
		}
		catch
		{
			var text = await File.ReadAllTextAsync(path);
			text = text.Trim();
			try
			{
				psbt = PSBT.Parse(text, network);
			}
			catch
			{
				return new SmartTransaction(Transaction.Parse(text, network), Height.Unknown);
			}
		}

		if (!psbt.IsAllFinalized())
		{
			psbt.Finalize();
		}

		return psbt.ExtractSmartTransaction();
	}

	public static async Task<bool> ExportTransactionToBinaryAsync(BuildTransactionResult transaction)
	{
		var psbtExtension = "psbt";
		var filePath = await FileDialogHelper.ShowSaveFileDialogAsync("Export transaction", new[] { psbtExtension });

		if (!string.IsNullOrWhiteSpace(filePath))
		{
			var ext = Path.GetExtension(filePath);
			if (string.IsNullOrWhiteSpace(ext))
			{
				filePath = $"{filePath}.{psbtExtension}";
			}
			await File.WriteAllBytesAsync(filePath, transaction.Psbt.ToBytes());

			return true;
		}

		return false;
	}
}
