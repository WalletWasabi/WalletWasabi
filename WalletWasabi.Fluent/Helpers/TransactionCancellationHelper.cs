using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class TransactionCancellationHelper
{
	public static SmartTransaction CreateCancellation(SmartTransaction transactionToCancel, Wallet wallet)
	{
		var keyManager = wallet.KeyManager;
		var change = transactionToCancel.GetWalletOutputs(keyManager).FirstOrDefault();
		var originalFeeRate = transactionToCancel.Transaction.GetFeeRate(transactionToCancel.GetWalletInputs(keyManager).Select(x => x.Coin).Cast<ICoin>().ToArray());
		var cancelFeeRate = new FeeRate(originalFeeRate.SatoshiPerByte + Money.Satoshis(2).Satoshi);
		var originalTransaction = transactionToCancel.Transaction;
		var cancelTransaction = originalTransaction.Clone();
		cancelTransaction.Outputs.Clear();

		if (change is not null)
		{
			// IF change present THEN make the change the only output
			// Add a dummy output to make the transaction size proper.
			cancelTransaction.Outputs.Add(Money.Zero, change.TxOut.ScriptPubKey);
			var cancelFee = (long)(originalTransaction.GetVirtualSize() * cancelFeeRate.SatoshiPerByte) + 1;
			cancelTransaction.Outputs.Clear();
			cancelTransaction.Outputs.Add(transactionToCancel.GetWalletInputs(keyManager).Sum(x => x.Amount.Satoshi) - cancelFee, change.TxOut.ScriptPubKey);
		}
		else
		{
			// ELSE THEN replace the output with a new output that's ours
			// Add a dummy output to make the transaction size proper.
			var newOwnOutput = keyManager.GetNextChangeKey();
			cancelTransaction.Outputs.Add(Money.Zero, newOwnOutput.GetAssumedScriptPubKey());
			var cancelFee = (long)(originalTransaction.GetVirtualSize() * cancelFeeRate.SatoshiPerByte) + 1;
			cancelTransaction.Outputs.Clear();
			cancelTransaction.Outputs.Add(transactionToCancel.GetWalletInputs(keyManager).Sum(x => x.Amount.Satoshi) - cancelFee, newOwnOutput.GetAssumedScriptPubKey());
		}

		// Signing
		var signedCancelTransaction = SignTransaction(cancelTransaction, wallet, transactionToCancel);

		var signedCancelSmartTransaction = new SmartTransaction(
			signedCancelTransaction,
			Height.Mempool,
			isReplacement: true);

		foreach (var input in transactionToCancel.WalletInputs)
		{
			signedCancelSmartTransaction.TryAddWalletInput(input);
		}

		return signedCancelSmartTransaction;
	}

	private static Transaction SignTransaction(Transaction transactionToSign, Wallet wallet, SmartTransaction originalTransaction)
	{
		var keyManager = wallet.KeyManager;
		var hdkeys = keyManager.GetSecrets(wallet.Kitchen.SaltSoup(), originalTransaction.WalletInputs.Select(x => x.ScriptPubKey).ToArray()).ToArray();
		var secrets = new List<ISecret>();
		for (var i = 0; i < originalTransaction.WalletInputs.Count; i++)
		{
			var walletInput = originalTransaction.WalletInputs.ToArray()[i];
			var k = hdkeys[i];
			var secret = k.GetBitcoinSecret(keyManager.GetNetwork(), walletInput.ScriptPubKey);

			secrets.Add(secret);
		}

		var builder = wallet.Network.CreateTransactionBuilder();
		builder.AddKeys(secrets.ToArray());
		builder.AddCoins(originalTransaction.WalletInputs.Select(x => x.Coin));
		var signedCancelTransaction = builder.SignTransaction(transactionToSign);
		return signedCancelTransaction;
	}
}
