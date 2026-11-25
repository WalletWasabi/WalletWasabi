using NBitcoin.Policy;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Exceptions;
using WalletWasabi.Wallets.SilentPayment;

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
		Func<LockTime>? lockTimeSelector = null)
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

		// For sub-1 sat/vB fee rates, NBitcoin's FeeRate truncates because _FeePerK is a long
		// (e.g. 0.1 sat/vB × 141 vB = 14.1 -> 14 sats -> effective 0.099 sat/vB).
		// Rebuild with a FeeRate whose _FeePerK is ceiled to guarantee the fee covers the target.
		if (parameters.FeeRate.SatoshiPerByte < 1m && psbt.TryGetVirtualSize(out var estimatedVSize))
		{
			var adjustedFee = parameters.FeeRate.GetAdjustedFee(estimatedVSize);
			var truncatedFee = parameters.FeeRate.GetFee(estimatedVSize);
			if (truncatedFee < adjustedFee)
			{
				var ceilFeePerK = (long)Math.Ceiling(adjustedFee.Satoshi * 1000m / estimatedVSize);
				builder.SendEstimatedFees(new FeeRate(Money.Satoshis(ceilFeePerK)));
				psbt = builder.BuildPSBT(false);
			}
		}

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
			var signingKeysArray = signingKeys.ToArray();
			builder.AddKeys(signingKeysArray);

			// Resolve silent payment outputs using BIP-375 PSBT info
			// this is temporary, the signing device are responsible fo resolving the real payment output.
			if (psbt.HasSilentPaymentOutputs())
			{
				psbt = psbt.ResolveSilentPaymentOutputs(spk => signingKeysArray.FirstOrDefault(k =>
					k.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86) == spk ||
					k.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit) == spk));
			}

			builder.SignPSBT(psbt);

			psbt.Finalize();
			tx = psbt.ExtractTransaction();

			var checkResults = builder.Check(tx).ToList();
			if (checkResults.Count > 0)
			{
				Logger.LogDebug($"Found policy error(s)! First error: '{checkResults[0]}'.");
				throw new InvalidTxException(tx, checkResults);
			}
		}

		var smartTransaction = new SmartTransaction(tx, labels: LabelsArray.Merge(payments.Requests.Select(x => x.Labels)));
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
}

public class TransactionBuilderWithSilentPaymentSupport
{
	private readonly TransactionBuilder _builder;
	private readonly List<SilentPaymentAddress> _silentPaymentOutputs = [];

	public TransactionBuilderWithSilentPaymentSupport(Network network)
	{
		_builder = network.CreateTransactionBuilder();
		_builder.SetVersion(2);
		_builder.StandardTransactionPolicy.MinRelayTxFee = Constants.MinRelayFeeRate;
	}

	public Func<OutPoint, ICoin>? CoinFinder
	{
		set => _builder.CoinFinder = value;
	}

	public void SetCoinSelector(ICoinSelector coinSelector) =>
		_builder.SetCoinSelector(coinSelector);

	public void AddCoins(IEnumerable<Coin> coins) =>
		_builder.AddCoins(coins);

	public void SetLockTime(LockTime lockTime) =>
		_builder.SetLockTime(lockTime);

	public void SubtractFees() =>
		_builder.SubtractFees();

	public void SendEstimatedFees(FeeRate feeRate) =>
		_builder.SendEstimatedFees(feeRate);

	public void AddKeys(Key[] keys) =>
		_builder.AddKeys(keys);

	public void SignPSBT(PSBT psbt) =>
		_builder.SignPSBT(psbt);

	public TransactionPolicyError[] Check(Transaction tx) =>
		_builder.Check(tx);

	public int EstimateSize(Transaction tx, bool virtualSize) =>
		_builder.EstimateSize(tx, virtualSize);

	public void Send(Destination destination, Money amount)
	{
		switch (destination)
		{
			case Destination.Loudly l:
				_builder.Send(l.ScriptPubKey, amount);
				break;

			case Destination.Silent s:
				_builder.Send(Destination.SilentPaymentPlaceholderScript, amount);
				_silentPaymentOutputs.Add(s.Address);
				break;
		}
	}

	public void SetChange(Destination destination)
	{
		switch (destination)
		{
			case Destination.Loudly l:
				_builder.SetChange(l.ScriptPubKey);
				break;

			case Destination.Silent s:
				_builder.SetChange(Destination.SilentPaymentPlaceholderScript);
				_silentPaymentOutputs.Add(s.Address);
				break;
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
				_builder.SendAllRemaining(Destination.SilentPaymentPlaceholderScript);
				_silentPaymentOutputs.Add(s.Address);
				break;
		}
	}

	public PSBT BuildPSBT(bool sign)
	{
		var psbt = _builder.BuildPSBT(sign, PSBTVersion.PSBTv2);

		var spIndex = 0;
		foreach (var output in psbt.Outputs)
		{
			if (output.ScriptPubKey == Destination.SilentPaymentPlaceholderScript)
			{
				if (spIndex >= _silentPaymentOutputs.Count)
				{
					throw new InvalidOperationException("Mismatch between placeholder outputs and tracked silent payment addresses.");
				}
				output.SetSilentPaymentInfo(_silentPaymentOutputs[spIndex]);
				spIndex++;
			}
		}

		return psbt;
	}
}
