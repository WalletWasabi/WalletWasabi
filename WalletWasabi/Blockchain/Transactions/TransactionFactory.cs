using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using NBitcoin.Policy;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets.SilentPayment;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Blockchain.Transactions;

public class TransactionFactory
{
	public TransactionFactory(Network network, KeyManager keyManager, ICoinsView coins, ITransactionStore transactionStore, string password = "")
	{
		Network = network;
		KeyManager = keyManager;
		Coins = coins;
		_transactionStore = transactionStore;
		_password = password;
	}

	public Network Network { get; }
	public KeyManager KeyManager { get; }
	public ICoinsView Coins { get; }
	private readonly string _password;
	private readonly ITransactionStore _transactionStore;

	public BuildTransactionResult BuildTransaction(
		TransactionParameters parameters,
		Func<LockTime>? lockTimeSelector = null,
		IPayjoinClient? payjoinClient = null)
	{
		lockTimeSelector ??= () => LockTime.Zero;

		var payments = parameters.PaymentIntent;
		long totalAmount = payments.TotalAmount.Satoshi;
		if (totalAmount is < 0 or > Constants.MaximumNumberOfSatoshis)
		{
			throw new ArgumentOutOfRangeException($"{nameof(payments)}.{nameof(payments.TotalAmount)} sum cannot be smaller than 0 or greater than {Constants.MaximumNumberOfSatoshis}.");
		}

		var isSilentPayment = payments.Requests.Select(x => x.Destination).OfType<Destination.Silent>().Any();
		var canUsePrivateKeys = !KeyManager.IsWatchOnly;
		if (isSilentPayment && !canUsePrivateKeys)
		{
			throw new InvalidOperationException("Silent payments requires a hot wallet.");
		}

		// Get allowed coins to spend.
		var availableCoinsView = Coins.Unspent();
		if (parameters.AllowDoubleSpend && parameters.AllowedInputs is not null)
		{
			var doubleSpends = new List<SmartCoin>();
			foreach (var input in parameters.AllowedInputs)
			{
				if (((CoinsRegistry)Coins).AsAllCoinsView().TryGetByOutPoint(input, out var coin)
					&& coin.SpenderTransaction is not null
					&& !coin.SpenderTransaction.Confirmed)
				{
					doubleSpends.Add(coin);
				}
			}
			availableCoinsView = new CoinsView(availableCoinsView.ToList().Concat(doubleSpends));
		}

		List<SmartCoin> allowedSmartCoinInputs = parameters.AllowUnconfirmed // Inputs that can be used to build the transaction.
				? availableCoinsView.ToList()
				: availableCoinsView.Confirmed().ToList();
		if (parameters.AllowedInputs is not null) // If allowedInputs are specified then select the coins from them.
		{
			if (!parameters.AllowedInputs.Any())
			{
				throw new ArgumentException($"{nameof(parameters.AllowedInputs)} is not null, but empty.");
			}

			allowedSmartCoinInputs = allowedSmartCoinInputs
				.Where(x => parameters.AllowedInputs.Any(y => y.Hash == x.TransactionId && y.N == x.Index))
				.ToList();

			// Add those that have the same script, because common ownership is already exposed.
			// But only if the user didn't click the "max" button. In this case he'd send more money than what he'd think.
			if (payments.ChangeStrategy != ChangeStrategy.AllRemainingCustom)
			{
				var allScripts = allowedSmartCoinInputs.Select(x => x.ScriptPubKey).ToHashSet();
				foreach (var coin in availableCoinsView.Where(x => !allowedSmartCoinInputs.Any(y => x.TransactionId == y.TransactionId && x.Index == y.Index)))
				{
					if (!(parameters.AllowUnconfirmed || coin.Confirmed))
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

		var builder = new TransactionBuilderWithSilentPaymentSupport(Network);
		builder.SetCoinSelector(new SmartCoinSelector(allowedSmartCoinInputs));
		builder.AddCoins(allowedSmartCoinInputs.Select(c => c.Coin));
		builder.SetLockTime(lockTimeSelector());

		foreach (var request in payments.Requests.Where(x => x.Amount is MoneyRequest.Value).Select(x => (x.Destination, Amount: (MoneyRequest.Value)x.Amount, x.Amount.SubtractFee)))
		{
			builder.Send(request.Destination, request.Amount.Amount);
			if (request.SubtractFee)
			{
				builder.SubtractFees();
			}
		}

		HdPubKey? changeHdPubKey;

		if (payments.TryGetCustomRequest(out DestinationRequest? customChange))
		{
			var changeScript = customChange.Destination.GetScriptPubKey();
			KeyManager.TryGetKeyForScriptPubKey(changeScript, out HdPubKey? hdPubKey);
			changeHdPubKey = hdPubKey;

			var changeStrategy = payments.ChangeStrategy;
			if (changeStrategy == ChangeStrategy.Custom)
			{
				builder.SetChange(customChange.Destination);
			}
			else if (changeStrategy == ChangeStrategy.AllRemainingCustom)
			{
				builder.SendAllRemaining(customChange.Destination);
			}
			else
			{
				throw new NotSupportedException(payments.ChangeStrategy.ToString());
			}
		}
		else
		{
			changeHdPubKey = KeyManager.GetNextChangeKey();

			builder.SetChange(changeHdPubKey.GetAssumedScriptPubKey());
		}

		builder.SendEstimatedFees(parameters.FeeRate);

		var psbt = builder.BuildPSBT(false);

		var spentCoins = psbt.Inputs.Select(txin => allowedSmartCoinInputs.First(y => y.Outpoint == txin.PrevOut)).ToArray();

		var realToSend = payments.Requests
			.Select(t =>
				(label: t.Labels,
					destination: t.Destination,
					amount: psbt.Outputs.FirstOrDefault(o => o.ScriptPubKey == t.Destination.GetScriptPubKey())?.Value))
			.Where(x => x.amount is not null);

		if (!psbt.TryGetFee(out var fee))
		{
			throw new InvalidOperationException("Impossible to get the fees of the PSBT, this should never happen.");
		}

		if (!psbt.TryGetVirtualSize(out var vSize)) //builder.EstimateSize(psbt.ExtractTransaction(), true);
		{
			throw new InvalidOperationException("It was not possible to estimate the size of the transaction");
		}

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
			totalOutgoingAmountNoFee = realToSend.Where(x => !changeHdPubKey.ContainsScript(x.destination.GetScriptPubKey())).Sum(x => x.amount);
		}

		decimal totalOutgoingAmountNoFeeDecimal = totalOutgoingAmountNoFee.ToDecimal(MoneyUnit.BTC);
		decimal feeDecimal = fee.ToDecimal(MoneyUnit.BTC);

		decimal feePercentage;
		if (payments.ChangeStrategy == ChangeStrategy.AllRemainingCustom)
		{
			// In this scenario since the amount changes as the fee changes, we need to compare against the total sum / 2,
			// as with this, we will make sure the fee cannot be higher than the amount.
			decimal inputSumDecimal = spentCoins.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC));
			feePercentage = 100 * (feeDecimal / (inputSumDecimal / 2));
		}
		else
		{
			// In this scenario the amount is fixed, so we can compare against it.
			// Cannot divide by zero, so use the closest number we have to zero.
			decimal totalOutgoingAmountNoFeeDecimalDivisor = totalOutgoingAmountNoFeeDecimal == 0 ? decimal.MinValue : totalOutgoingAmountNoFeeDecimal;
			feePercentage = 100 * (feeDecimal / totalOutgoingAmountNoFeeDecimalDivisor);
		}
		if (feePercentage > 100 && !parameters.OverrideFeeOverpaymentProtection)
		{
			throw new TransactionFeeOverpaymentException(feePercentage);
		}

		// Build the transaction

		// It must be watch only, too, because if we have the key and also hardware wallet, we do not care we can sign.
		psbt.AddKeyPaths(KeyManager);
		psbt.AddPrevTxs(_transactionStore);

		Transaction tx;
		if (KeyManager.IsWatchOnly || !parameters.TryToSign)
		{
			tx = psbt.GetGlobalTransaction();
		}
		else
		{
			IEnumerable<Key> signingKeys = KeyManager.GetSecrets(_password, spentCoins.Select(x => x.ScriptPubKey).ToArray());
			builder = builder.AddKeys(signingKeys.ToArray());

			psbt = builder.SolveSilentPayment(psbt);
			builder.SignPSBT(psbt);

			// Try to pay using payjoin
			if (payjoinClient is not null)
			{
				psbt = TryNegotiatePayjoin(payjoinClient, builder, psbt, changeHdPubKey);
				psbt.AddKeyPaths(KeyManager);
				psbt.AddPrevTxs(_transactionStore);
			}

			psbt.Finalize();
			tx = psbt.ExtractTransaction();

			if (payjoinClient is not null)
			{
				builder.CoinFinder = (outpoint) => psbt.Inputs.Select(x => x.GetCoin()).Single(x => x?.Outpoint == outpoint)!;
			}

			var checkResults = builder.Check(tx).ToList();
			if (checkResults.Count > 0)
			{
				Logger.LogDebug($"Found policy error(s)! First error: '{checkResults[0]}'.");
				throw new InvalidTxException(tx, checkResults);
			}
		}

		var smartTransaction = new SmartTransaction(tx, Height.Unknown, labels: LabelsArray.Merge(payments.Requests.Select(x => x.Labels)));
		foreach (var coin in spentCoins)
		{
			smartTransaction.TryAddWalletInput(coin);
		}
		var label = LabelsArray.Merge(payments.Requests.Select(x => x.Labels).Concat(smartTransaction.WalletInputs.Select(x => x.HdPubKey.Labels)));

		for (var i = 0U; i < tx.Outputs.Count; i++)
		{
			TxOut output = tx.Outputs[i];
			if (KeyManager.TryGetKeyForScriptPubKey(output.ScriptPubKey, out HdPubKey? foundKey))
			{
				var smartCoin = new SmartCoin(smartTransaction, i, foundKey);
				label = LabelsArray.Merge(label, smartCoin.HdPubKey.Labels); // foundKey's label is already added to the coinLabel.
				smartTransaction.TryAddWalletOutput(smartCoin);
			}
		}

		// New labels will be added to the HdPubKey only when tx will be successfully broadcasted.
		Dictionary<HdPubKey, LabelsArray> hdPubKeysWithNewLabels = new();

		foreach (var coin in smartTransaction.WalletOutputs)
		{
			var foundPaymentRequest = payments.Requests.FirstOrDefault(x => x.Destination.GetScriptPubKey() == coin.ScriptPubKey);

			// If change then we concatenate all the labels.
			// The foundKeyLabel has already been added previously, so no need to concatenate.
			if (foundPaymentRequest is null) // Then it's auto-change.
			{
				hdPubKeysWithNewLabels.Add(coin.HdPubKey, label);
			}
			else
			{
				hdPubKeysWithNewLabels.Add(coin.HdPubKey, LabelsArray.Merge(coin.HdPubKey.Labels, foundPaymentRequest.Labels));
			}
		}

		var sign = !KeyManager.IsWatchOnly;

		Logger.LogDebug($"Built tx: {totalOutgoingAmountNoFee.ToString(fplus: false, trimExcessZero: true)} BTC. Fee: {fee.Satoshi} sats. Vsize: {vSize} vBytes. Fee/Total ratio: {feePercentage:0.#}%. Tx hash: {tx.GetHash()}.");
		return new BuildTransactionResult(smartTransaction, psbt, sign, fee, feePercentage, hdPubKeysWithNewLabels);
	}

	private PSBT TryNegotiatePayjoin(IPayjoinClient payjoinClient, TransactionBuilderWithSilentPaymentSupport builder, PSBT psbt, HdPubKey changeHdPubKey)
	{
		try
		{
			Logger.LogInfo($"Negotiating payjoin payment with `{payjoinClient.PaymentUrl}`.");

			psbt = payjoinClient.RequestPayjoin(
				psbt,
				KeyManager.SegwitExtPubKey,
				new RootedKeyPath(KeyManager.MasterFingerprint.Value, KeyManager.SegwitAccountKeyPath),
				changeHdPubKey,
				CancellationToken.None).GetAwaiter().GetResult(); // WTF??!
			builder.SignPSBT(psbt);

			Logger.LogInfo("Payjoin payment was negotiated successfully.");
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

public class TransactionBuilderWithSilentPaymentSupport(Network network)
{
	private readonly TransactionBuilder _builder = network.CreateTransactionBuilder();
	private readonly Dictionary<Script, SilentPaymentAddress> _silentPayments = [];
	private Key[] _keys;

	public Func<OutPoint, ICoin> CoinFinder
	{
		get => _builder.CoinFinder;
		set => _builder.CoinFinder = value;
	}

	public void SetCoinSelector(ICoinSelector coinSelector)
	{
		_builder.SetCoinSelector(coinSelector);
	}

	public void AddCoins(IEnumerable<Coin> coins)
	{
		_builder.AddCoins(coins);
	}

	public void SetLockTime(LockTime lockTime)
	{
		_builder.SetLockTime(lockTime);
	}

	public void Send(Destination destination, Money amount)
	{
		switch (destination)
		{
			case Destination.Loudly l:
				_builder.Send(l.ScriptPubKey, amount);
				break;

			case Destination.Silent s:
				{
					_builder.Send(s.FakeScriptPubKey, amount);
					_silentPayments.Add(s.FakeScriptPubKey, s.Address);
					break;
				}
		}
	}

	public void SubtractFees()
	{
		_builder.SubtractFees();
	}

	public void SetChange(Destination destination)
	{
		switch (destination)
		{
			case Destination.Loudly l:
				_builder.SetChange(l.ScriptPubKey);
				break;

			case Destination.Silent s:
				{
					_builder.SetChange(s.FakeScriptPubKey);
					_silentPayments.Add(s.FakeScriptPubKey, s.Address);
					break;
				}
		}
	}

	public void SendAllRemaining(Destination destination)
	{
		switch (destination)
		{
			case Destination.Loudly l:
				_builder.SendAllRemaining(l.ScriptPubKey);
				break;

			case Destination.Silent s:
				{
					_builder.SendAllRemaining(s.FakeScriptPubKey);
					_silentPayments.Add(s.FakeScriptPubKey, s.Address);
					break;
				}
		}
	}

	public void SendEstimatedFees(FeeRate feeRate)
	{
		_builder.SendEstimatedFees(feeRate);
	}

	public PSBT BuildPSBT(bool sign)
	{
		return _builder.BuildPSBT(sign);
	}

	public int EstimateSize(Transaction tx, bool virtualSize)
	{
		return _builder.EstimateSize(tx, virtualSize);
	}

	public TransactionBuilderWithSilentPaymentSupport AddKeys(Key[] keys)
	{
		_keys = keys;
		_builder.AddKeys(keys);
		return this;
	}

	public void SignPSBT(PSBT psbt)
	{
		_builder.SignPSBT(psbt);
	}

	public TransactionPolicyError[] Check(Transaction tx)
	{
		return _builder.Check(tx);
	}

	public PSBT SolveSilentPayment(PSBT psbt)
	{
		var keys = _keys;

		Key GetKeyForScriptPubKey(Script spk)
		{
			foreach (var key in keys)
			{
				if (key.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86) == spk)
				{
					return key.Tweak();
				}
				if (key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit) == spk)
				{
					return key;
				}
			}

			throw new InvalidOperationException("Key not found for script pub key");
		}

		var spentCoins = psbt.Inputs
			.Select(x => x.GetCoin())
			.Select(x => new Utxo(x.Outpoint, GetKeyForScriptPubKey(x.ScriptPubKey), x.ScriptPubKey))
			.ToArray();
		var paymentAddresses = _silentPayments.Select(x => x.Value);
		var scriptPubKeys = SilentPayment
			.GetPubKeys(paymentAddresses, spentCoins)
			.Select(x => (SilentPaymentAddress: x.Key, SilentPaymentPubKey: x.Value.First()))
			.Select(x => (x.SilentPaymentAddress, TaprootPubKey: new TaprootPubKey(x.SilentPaymentPubKey.ToBytes())))
			.ToDictionary(x => x.SilentPaymentAddress, x => x.TaprootPubKey.ScriptPubKey);

		var tx = psbt.GetGlobalTransaction();
		foreach (var output in tx.Outputs)
		{
			if (_silentPayments.TryGetValue(output.ScriptPubKey, out var silentPaymentAddress))
			{
				output.ScriptPubKey = scriptPubKeys[silentPaymentAddress];
			}
		}

		var newPsbt = tx.CreatePSBT(network);

		foreach (var (newInput, oldInput) in newPsbt.Inputs.Zip(psbt.Inputs))
		{
			newInput.UpdateFrom(oldInput);
		}
		return newPsbt;
	}
}
