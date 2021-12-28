using NBitcoin;
using NBitcoin.Policy;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Blockchain.Transactions
{
	public class TransactionFactory
	{
		/// <param name="allowUnconfirmed">Allow to spend unconfirmed transactions, if necessary.</param>
		public TransactionFactory(Network network, KeyManager keyManager, ICoinsView coins, AllTransactionStore transactionStore, string password = "", bool allowUnconfirmed = false)
		{
			Network = network;
			KeyManager = keyManager;
			Coins = coins;
			TransactionStore = transactionStore;
			Password = password;
			AllowUnconfirmed = allowUnconfirmed;
		}

		public Network Network { get; }
		public KeyManager KeyManager { get; }
		public ICoinsView Coins { get; }
		public string Password { get; }
		public bool AllowUnconfirmed { get; }
		private AllTransactionStore TransactionStore { get; }

		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public BuildTransactionResult BuildTransaction(
			PaymentIntent payments,
			FeeRate feeRate,
			IEnumerable<OutPoint>? allowedInputs = null,
			IPayjoinClient? payjoinClient = null)
			=> BuildTransaction(payments, () => feeRate, allowedInputs, () => LockTime.Zero, payjoinClient);

		/// <exception cref="ArgumentException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public BuildTransactionResult BuildTransaction(
			PaymentIntent payments,
			Func<FeeRate> feeRateFetcher,
			IEnumerable<OutPoint>? allowedInputs = null,
			Func<LockTime>? lockTimeSelector = null,
			IPayjoinClient? payjoinClient = null)
		{
			lockTimeSelector ??= () => LockTime.Zero;

			long totalAmount = payments.TotalAmount.Satoshi;
			if (totalAmount is < 0 or > Constants.MaximumNumberOfSatoshis)
			{
				throw new ArgumentOutOfRangeException($"{nameof(payments)}.{nameof(payments.TotalAmount)} sum cannot be smaller than 0 or greater than {Constants.MaximumNumberOfSatoshis}.");
			}

			// Get allowed coins to spend.
			var availableCoinsView = Coins.Unspent();
			List<SmartCoin> allowedSmartCoinInputs = AllowUnconfirmed // Inputs that can be used to build the transaction.
					? availableCoinsView.ToList()
					: availableCoinsView.Confirmed().ToList();
			if (allowedInputs is not null) // If allowedInputs are specified then select the coins from them.
			{
				if (!allowedInputs.Any())
				{
					throw new ArgumentException($"{nameof(allowedInputs)} is not null, but empty.");
				}

				allowedSmartCoinInputs = allowedSmartCoinInputs
					.Where(x => allowedInputs.Any(y => y.Hash == x.TransactionId && y.N == x.Index))
					.ToList();

				// Add those that have the same script, because common ownership is already exposed.
				// But only if the user didn't click the "max" button. In this case he'd send more money than what he'd think.
				if (payments.ChangeStrategy != ChangeStrategy.AllRemainingCustom)
				{
					var allScripts = allowedSmartCoinInputs.Select(x => x.ScriptPubKey).ToHashSet();
					foreach (var coin in availableCoinsView.Where(x => !allowedSmartCoinInputs.Any(y => x.TransactionId == y.TransactionId && x.Index == y.Index)))
					{
						if (!(AllowUnconfirmed || coin.Confirmed))
						{
							continue;
						}

						if (allScripts.Contains(coin.ScriptPubKey))
						{
							allowedSmartCoinInputs.Add(coin);
						}
					}
				}
			}

			// Get and calculate fee
			Logger.LogInfo("Calculating dynamic transaction fee...");

			TransactionBuilder builder = Network.CreateTransactionBuilder();
			builder.SetCoinSelector(new SmartCoinSelector(allowedSmartCoinInputs));
			builder.AddCoins(allowedSmartCoinInputs.Select(c => c.Coin));
			builder.SetLockTime(lockTimeSelector());

			foreach (var request in payments.Requests.Where(x => x.Amount.Type == MoneyRequestType.Value))
			{
				var amountRequest = request.Amount;

				builder.Send(request.Destination, amountRequest.Amount);
				if (amountRequest.SubtractFee)
				{
					builder.SubtractFees();
				}
			}

			HdPubKey? changeHdPubKey;

			if (payments.TryGetCustomRequest(out DestinationRequest? custChange))
			{
				var changeScript = custChange.Destination.ScriptPubKey;
				KeyManager.TryGetKeyForScriptPubKey(changeScript, out HdPubKey? hdPubKey);
				changeHdPubKey = hdPubKey;

				var changeStrategy = payments.ChangeStrategy;
				if (changeStrategy == ChangeStrategy.Custom)
				{
					builder.SetChange(changeScript);
				}
				else if (changeStrategy == ChangeStrategy.AllRemainingCustom)
				{
					builder.SendAllRemaining(changeScript);
				}
				else
				{
					throw new NotSupportedException(payments.ChangeStrategy.ToString());
				}
			}
			else
			{
				KeyManager.AssertCleanKeysIndexed(isInternal: true);
				KeyManager.AssertLockedInternalKeysIndexed(14);
				changeHdPubKey = KeyManager.GetKeys(KeyState.Clean, true).First();

				builder.SetChange(changeHdPubKey.P2wpkhScript);
			}

			builder.OptInRBF = true;

			builder.SendEstimatedFees(feeRateFetcher());

			var psbt = builder.BuildPSBT(false);

			var spentCoins = psbt.Inputs.Select(txin => allowedSmartCoinInputs.First(y => y.OutPoint == txin.PrevOut)).ToArray();

			var realToSend = payments.Requests
				.Select(t =>
					(label: t.Label,
					destination: t.Destination,
					amount: psbt.Outputs.FirstOrDefault(o => o.ScriptPubKey == t.Destination.ScriptPubKey)?.Value))
				.Where(i => i.amount is not null);

			if (!psbt.TryGetFee(out var fee))
			{
				throw new InvalidOperationException("Impossible to get the fees of the PSBT, this should never happen.");
			}
			Logger.LogInfo($"Fee: {fee.Satoshi} Satoshi.");

			var vSize = builder.EstimateSize(psbt.GetOriginalTransaction(), true);
			Logger.LogInfo($"Estimated tx size: {vSize} vBytes.");

			// Do some checks
			Money totalSendAmountNoFee = realToSend.Sum(x => x.amount);
			if (totalSendAmountNoFee == Money.Zero)
			{
				throw new InvalidOperationException("The amount after subtracting the fee is too small to be sent.");
			}

			Money totalOutgoingAmountNoFee;
			if (changeHdPubKey is null)
			{
				totalOutgoingAmountNoFee = totalSendAmountNoFee;
			}
			else
			{
				totalOutgoingAmountNoFee = realToSend.Where(x => !changeHdPubKey.ContainsScript(x.destination.ScriptPubKey)).Sum(x => x.amount);
			}
			decimal totalOutgoingAmountNoFeeDecimal = totalOutgoingAmountNoFee.ToDecimal(MoneyUnit.BTC);

			// Cannot divide by zero, so use the closest number we have to zero.
			decimal totalOutgoingAmountNoFeeDecimalDivisor = totalOutgoingAmountNoFeeDecimal == 0 ? decimal.MinValue : totalOutgoingAmountNoFeeDecimal;
			decimal feePc = 100 * fee.ToDecimal(MoneyUnit.BTC) / totalOutgoingAmountNoFeeDecimalDivisor;

			if (feePc > 1)
			{
				Logger.LogInfo($"The transaction fee is {feePc:0.#}% of the sent amount.{Environment.NewLine}"
					+ $"Sending:\t {totalOutgoingAmountNoFee.ToString(fplus: false, trimExcessZero: true)} BTC.{Environment.NewLine}"
					+ $"Fee:\t\t {fee.Satoshi} Satoshi.");
			}
			if (feePc > 100)
			{
				throw new TransactionFeeOverpaymentException(feePc);
			}

			if (spentCoins.Any(u => !u.Confirmed))
			{
				Logger.LogInfo("Unconfirmed transaction is spent.");
			}

			// Build the transaction
			Logger.LogInfo("Signing transaction...");

			// It must be watch only, too, because if we have the key and also hardware wallet, we do not care we can sign.
			psbt.AddKeyPaths(KeyManager);
			psbt.AddPrevTxs(TransactionStore);

			Transaction tx;
			if (KeyManager.IsWatchOnly)
			{
				tx = psbt.GetGlobalTransaction();
			}
			else
			{
				IEnumerable<ExtKey> signingKeys = KeyManager.GetSecrets(Password, spentCoins.Select(x => x.ScriptPubKey).ToArray());
				builder = builder.AddKeys(signingKeys.ToArray());
				builder.SignPSBT(psbt);

				// Try to pay using payjoin
				if (payjoinClient is not null)
				{
					psbt = TryNegotiatePayjoin(payjoinClient, builder, psbt, changeHdPubKey);
					psbt.AddKeyPaths(KeyManager);
					psbt.AddPrevTxs(TransactionStore);
				}

				psbt.Finalize();
				tx = psbt.ExtractTransaction();

				var checkResults = builder.Check(tx).ToList();
				if (checkResults.Count > 0)
				{
					Logger.LogDebug($"Found policy error(s)! First error: '{checkResults[0]}'.");
					throw new InvalidTxException(tx, checkResults);
				}
			}

			var smartTransaction = new SmartTransaction(tx, Height.Unknown, label: SmartLabel.Merge(payments.Requests.Select(x => x.Label)));
			foreach (var coin in spentCoins)
			{
				smartTransaction.WalletInputs.Add(coin);
			}
			var label = SmartLabel.Merge(payments.Requests.Select(x => x.Label).Concat(smartTransaction.WalletInputs.Select(x => x.HdPubKey.Label)));

			for (var i = 0U; i < tx.Outputs.Count; i++)
			{
				TxOut output = tx.Outputs[i];
				if (KeyManager.TryGetKeyForScriptPubKey(output.ScriptPubKey, out HdPubKey? foundKey))
				{
					var smartCoin = new SmartCoin(smartTransaction, i, foundKey);
					label = SmartLabel.Merge(label, smartCoin.HdPubKey.Label); // foundKey's label is already added to the coinlabel.
					smartTransaction.WalletOutputs.Add(smartCoin);
				}
			}

			foreach (var coin in smartTransaction.WalletOutputs)
			{
				var foundPaymentRequest = payments.Requests.FirstOrDefault(x => x.Destination.ScriptPubKey == coin.ScriptPubKey);

				// If change then we concatenate all the labels.
				// The foundkeylabel has already been added previously, so no need to concatenate.
				if (foundPaymentRequest is null) // Then it's autochange.
				{
					coin.HdPubKey.SetLabel(label);
				}
				else
				{
					coin.HdPubKey.SetLabel(SmartLabel.Merge(coin.HdPubKey.Label, foundPaymentRequest.Label));
				}
			}

			Logger.LogInfo($"Transaction is successfully built: {tx.GetHash()}.");
			var sign = !KeyManager.IsWatchOnly;
			return new BuildTransactionResult(smartTransaction, psbt, sign, fee, feePc);
		}

		private PSBT TryNegotiatePayjoin(IPayjoinClient payjoinClient, TransactionBuilder builder, PSBT psbt, HdPubKey changeHdPubKey)
		{
			try
			{
				Logger.LogInfo($"Negotiating payjoin payment with `{payjoinClient.PaymentUrl}`.");

				psbt = payjoinClient.RequestPayjoin(
					psbt,
					KeyManager.ExtPubKey,
					new RootedKeyPath(KeyManager.MasterFingerprint.Value, KeyManager.AccountKeyPath),
					changeHdPubKey,
					CancellationToken.None).GetAwaiter().GetResult(); // WTF??!
				builder.SignPSBT(psbt);

				Logger.LogInfo("Payjoin payment was negotiated successfully.");
			}
			catch (HttpRequestException ex) when (ex.InnerException is TorConnectCommandFailedException innerEx)
			{
				if (innerEx.Message.Contains("HostUnreachable"))
				{
					Logger.LogWarning("Payjoin server is not reachable. Ignoring...");
				}

				// Ignore.
			}
			catch (HttpRequestException e)
			{
				Logger.LogWarning($"Payjoin server responded with {e.ToTypeMessageString()}. Ignoring...");
			}
			catch (PayjoinException e)
			{
				Logger.LogWarning($"Payjoin server responded with {e.Message}. Ignoring...");
			}

			return psbt;
		}
	}
}
