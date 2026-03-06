using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class TransactionHelpers
{
	public static BuildTransactionResult BuildTransaction(Wallet wallet, TransactionInfo transactionInfo, bool isPayJoin = false, bool tryToSign = true)
	{
		if (transactionInfo.IsPayToMany)
		{
			return BuildPayToManyTransaction(wallet, transactionInfo, tryToSign);
		}

		if (transactionInfo.IsOptimized)
		{
			return wallet.BuildChangelessTransaction(
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

		return wallet.BuildTransaction(
			transactionInfo.Destination,
			transactionInfo.Amount,
			transactionInfo.Recipient,
			transactionInfo.FeeRate,
			transactionInfo.Coins,
			transactionInfo.SubtractFee,
			isPayJoin ? transactionInfo.PayJoinClient : null,
			tryToSign: tryToSign);
	}

	private static BuildTransactionResult BuildPayToManyTransaction(Wallet wallet, TransactionInfo transactionInfo, bool tryToSign)
	{
		var intent = BuildPayToManyIntent(transactionInfo);

		return wallet.BuildTransaction(
			password: wallet.Password,
			payments: intent,
			feeStrategy: FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
			allowUnconfirmed: true,
			allowedInputs: transactionInfo.Coins.Select(c => c.Outpoint),
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
		minimumAmount = transactionInfo.IsPayToMany ? transactionInfo.TotalAmount : transactionInfo.Amount;

		try
		{
			PaymentIntent intent;
			if (transactionInfo.IsPayToMany)
			{
				intent = BuildPayToManyIntent(transactionInfo);
			}
			else
			{
				intent = new PaymentIntent(
					destination: transactionInfo.Destination,
					amount: transactionInfo.Amount,
					subtractFee: transactionInfo.SubtractFee,
					label: transactionInfo.Recipient);
			}

			var network = keyManager.GetNetwork();
			var builder = new TransactionFactory(network, keyManager, allCoins, new EmptyTransactionStore(network), password);

			TransactionParameters parameters = new (
				intent,
				transactionInfo.FeeRate,
				AllowUnconfirmed: true,
				AllowDoubleSpend: false,
				AllowedInputs: allowedCoins.Select(x => x.Outpoint),
				TryToSign: false,
				OverrideFeeOverpaymentProtection: false);

			builder.BuildTransaction(
				parameters,
				lockTimeSelector: () => LockTime.Zero, // Doesn't matter.
				transactionInfo.PayJoinClient);

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
		catch (Exception ex)
		{
			// Couldn't parse to PSBT with bytes, try parsing with string.
			Logger.LogWarning($"Failed to load PSBT by bytes. Trying with string. {ex}");
			var text = await File.ReadAllTextAsync(path);
			text = text.Trim();
			try
			{
				psbt = PSBT.Parse(text, network);
			}
			catch (Exception exc)
			{
				// Couldn't parse to PSBT with string. All else failed, try to build SmartTransaction and broadcast that.
				Logger.LogWarning($"Failed to parse PSBT by string. Fall back to building SmartTransaction from the string. {exc}");
				return new SmartTransaction(Transaction.Parse(text, network), Height.Unknown);
			}
		}

		if (!psbt.IsAllFinalized())
		{
			psbt.Finalize();
		}

		return psbt.ExtractSmartTransaction();
	}

	internal static PaymentIntent BuildPayToManyIntent(TransactionInfo transactionInfo)
	{
		var requests = transactionInfo.AllRecipients
			.Select((r, index) =>
			{
				bool subtractFee = index == 0 ? transactionInfo.SubtractFee : r.IsSubtractFee;
				return new DestinationRequest(r.Destination, MoneyRequest.Create(r.Amount, subtractFee), r.Label);
			})
			.ToArray();

		return new PaymentIntent(requests);
	}

	public static async Task<bool> ExportTransactionToBinaryAsync(BuildTransactionResult transaction)
	{
		var psbtExtension = "psbt";
		string initialFileName = transaction.Transaction.GetHash().ToString();
		var file = await FileDialogHelper.SaveFileAsync("Export transaction", new[] { psbtExtension }, initialFileName);
		if (file is null)
		{
			return false;
		}

		var filePath = file.Path.LocalPath;

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
