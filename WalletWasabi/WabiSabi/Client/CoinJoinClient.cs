using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinClient
{
	private const int MaxInputsRegistrableByWallet = 10; // how many
	private volatile bool _inCriticalCoinJoinState;

	/// <param name="minAnonScoreTarget">Coins those have reached anonymity target, but still can be mixed if desired.</param>
	/// <param name="consolidationMode">If true, then aggressively try to consolidate as many coins as it can.</param>
	public CoinJoinClient(
		IWasabiHttpClientFactory httpClientFactory,
		IKeyChain keyChain,
		IDestinationProvider destinationProvider,
		RoundStateUpdater roundStatusUpdater,
		int minAnonScoreTarget = int.MaxValue,
		bool consolidationMode = false,
		TimeSpan feeRateMedianTimeFrame = default,
		TimeSpan doNotRegisterInLastMinuteTimeLimit = default)
	{
		HttpClientFactory = httpClientFactory;
		KeyChain = keyChain;
		DestinationProvider = destinationProvider;
		RoundStatusUpdater = roundStatusUpdater;
		MinAnonScoreTarget = minAnonScoreTarget;
		ConsolidationMode = consolidationMode;
		FeeRateMedianTimeFrame = feeRateMedianTimeFrame;
		SecureRandom = new SecureRandom();
		DoNotRegisterInLastMinuteTimeLimit = doNotRegisterInLastMinuteTimeLimit;
	}

	private SecureRandom SecureRandom { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	private IKeyChain KeyChain { get; }
	private IDestinationProvider DestinationProvider { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	public int MinAnonScoreTarget { get; }
	private TimeSpan DoNotRegisterInLastMinuteTimeLimit { get; }

	public bool InCriticalCoinJoinState
	{
		get => _inCriticalCoinJoinState;
		private set => _inCriticalCoinJoinState = value;
	}

	public bool ConsolidationMode { get; private set; }
	private TimeSpan FeeRateMedianTimeFrame { get; }

	public async Task<bool> StartCoinJoinAsync(IEnumerable<SmartCoin> coins, CancellationToken cancellationToken)
	{
		var currentRoundState = await RoundStatusUpdater
			.CreateRoundAwaiter(
				roundState =>
					roundState.InputRegistrationEnd - DateTimeOffset.UtcNow > DoNotRegisterInLastMinuteTimeLimit &&
					roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min < Money.Coins(0.0001m) && // ignore rounds with too big minimum denominations
					roundState.Phase == Phase.InputRegistration &&
		  IsRoundEconomic(roundState.FeeRate),
				cancellationToken)
			.ConfigureAwait(false);

		// This should be roughly log(#inputs), it could be set slightly
		// higher if more inputs are observed but that involves trusting the
		// coordinator with those values. Therefore, conservatively set this
		// so that a maximum of 6 blame rounds are executed.
		// FIXME should smaller rounds abort earlier?
		var tryLimit = 6;

		for (var tries = 0; tries < tryLimit; tries++)
		{
			if (await StartRoundAsync(coins, currentRoundState, cancellationToken).ConfigureAwait(false))
			{
				return true;
			}

			using CancellationTokenSource waitForBlameRound = new(TimeSpan.FromMinutes(5));
			using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(waitForBlameRound.Token, cancellationToken);

			var blameRoundState = await RoundStatusUpdater
				.CreateRoundAwaiter(
					roundState =>
						roundState.BlameOf == currentRoundState.Id &&
						roundState.Phase == Phase.InputRegistration,
					linkedTokenSource.Token)
				.ConfigureAwait(false);
			currentRoundState = blameRoundState;
		}

		return false;
	}

	/// <summary>Attempt to participate in a specified round.</summary>
	/// <param name="roundState">Defines the round parameter and state information to use.</param>
	/// <returns>Whether or not the round resulted in a successful transaction.</returns>
	public async Task<bool> StartRoundAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		var constructionState = roundState.Assert<ConstructionState>();

		var coinCandidates = SelectCoinsForRound(smartCoins, constructionState.Parameters, ConsolidationMode, MinAnonScoreTarget, SecureRandom);

		// Register coins.
		using PersonCircuit personCircuit = HttpClientFactory.NewHttpClientWithPersonCircuit(out Tor.Http.IHttpClient httpClient);

		var registeredAliceClients = await CreateRegisterAndConfirmCoinsAsync(httpClient, coinCandidates, roundState, cancellationToken).ConfigureAwait(false);
		if (!registeredAliceClients.Any())
		{
			Logger.LogInfo($"Round ({roundState.Id}): There is no available alices to participate with.");
			return true;
		}

		try
		{
			InCriticalCoinJoinState = true;

			// Calculate outputs values
			var registeredCoins = registeredAliceClients.Select(x => x.SmartCoin.Coin);
			var inputEffectiveValuesAndSizes = registeredAliceClients.Select(x => (x.EffectiveValue, x.SmartCoin.ScriptPubKey.EstimateInputVsize()));

			var availableVsize = registeredAliceClients.SelectMany(x => x.IssuedVsizeCredentials).Sum(x => x.Value);

			// Waiting for OutputRegistration phase, all the Alices confirmed their connections, so the list of the inputs will be complete.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);

			// Calculate outputs values
			constructionState = roundState.Assert<ConstructionState>();

			AmountDecomposer amountDecomposer = new(roundState.FeeRate, roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min, Constants.P2wpkhOutputSizeInBytes, (int)availableVsize);
			var theirCoins = constructionState.Inputs.Except(registeredCoins);
			var registeredCoinEffectiveValues = registeredAliceClients.Select(x => x.EffectiveValue);
			var theirCoinEffectiveValues = theirCoins.Select(x => x.EffectiveValue(roundState.FeeRate, roundState.CoordinationFeeRate));
			var outputValues = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues);

			// Get as many destinations as outputs we need.
			var destinations = DestinationProvider.GetNextDestinations(outputValues.Count()).ToArray();
			var outputTxOuts = outputValues.Zip(destinations, (amount, destination) => new TxOut(amount, destination.ScriptPubKey));

			DependencyGraph dependencyGraph = DependencyGraph.ResolveCredentialDependencies(inputEffectiveValuesAndSizes, outputTxOuts, roundState.FeeRate, roundState.CoordinationFeeRate, roundState.MaxVsizeAllocationPerAlice);
			DependencyGraphTaskScheduler scheduler = new(dependencyGraph);

			// Re-issuances.
			var bobClient = CreateBobClient(roundState);
			Logger.LogInfo($"Round ({roundState.Id}), Starting reissuances.");
			await scheduler.StartReissuancesAsync(registeredAliceClients, bobClient, cancellationToken).ConfigureAwait(false);

			// Output registration.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Output registration phase started.");

			await scheduler.StartOutputRegistrationsAsync(outputTxOuts, bobClient, KeyChain, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Outputs({outputTxOuts.Count()}) successfully registered.");

			// ReadyToSign.
			await ReadyToSignAsync(registeredAliceClients, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Alices({registeredAliceClients.Length}) successfully signalled ready to sign.");

			// Signing.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Transaction signing phase started.");

			var signingState = roundState.Assert<SigningState>();
			var unsignedCoinJoin = signingState.CreateUnsignedTransaction();

			// Sanity check.
			if (!SanityCheck(outputTxOuts, unsignedCoinJoin))
			{
				throw new InvalidOperationException($"Round ({roundState.Id}): My output is missing.");
			}

			// Send signature.
			await SignTransactionAsync(registeredAliceClients, unsignedCoinJoin, roundState, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Alices({registeredAliceClients.Length}) successfully signed the coinjoin tx.");

			var finalRoundState = await RoundStatusUpdater.CreateRoundAwaiter(s => s.Id == roundState.Id && s.Phase == Phase.Ended, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Round Ended - WasTransactionBroadcast: '{finalRoundState.WasTransactionBroadcast}'.");

			LogCoinJoinSummary(registeredAliceClients, outputTxOuts, unsignedCoinJoin, roundState);

			return finalRoundState.WasTransactionBroadcast;
		}
		finally
		{
			foreach (var aliceClient in registeredAliceClients)
			{
				aliceClient.Finish();
			}
			InCriticalCoinJoinState = false;
		}
	}

	private async Task<ImmutableArray<AliceClient>> CreateRegisterAndConfirmCoinsAsync(Tor.Http.IHttpClient httpClient, IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		async Task<AliceClient?> RegisterInputAsync(SmartCoin coin, CancellationToken cancellationToken)
		{
			try
			{
				// Alice client requests are inherently linkable to each other, so the circuit can be reused
				var arenaRequestHandler = new WabiSabiHttpApiClient(httpClient);

				var aliceArenaClient = new ArenaClient(
					roundState.CreateAmountCredentialClient(SecureRandom),
					roundState.CreateVsizeCredentialClient(SecureRandom),
					arenaRequestHandler);

				return await AliceClient.CreateRegisterAndConfirmInputAsync(roundState, aliceArenaClient, coin, KeyChain, RoundStatusUpdater, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		// Gets the list of scheduled dates/time in the remaining available time frame when each alice has to be registered.
		var remainingTimeForRegistration = (roundState.InputRegistrationEnd - DateTimeOffset.UtcNow);

		Logger.LogDebug($"Round ({roundState.Id}): Input registration started, it will end in {remainingTimeForRegistration:hh\\:mm\\:ss}.");

		var scheduledDates = remainingTimeForRegistration.SamplePoisson(smartCoins.Count());

		// Creates scheduled tasks (tasks that wait until the specified date/time and then perform the real registration)
		var aliceClients = smartCoins.Zip(
			scheduledDates,
			async (coin, date) =>
			{
				var delay = date - DateTimeOffset.UtcNow;
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
				return await RegisterInputAsync(coin, cancellationToken).ConfigureAwait(false);
			}).ToImmutableArray();

		await Task.WhenAll(aliceClients).ConfigureAwait(false);

		return aliceClients
			.Where(x => x.Result is not null)
			.Select(x => x.Result!)
			.ToImmutableArray();
	}

	private BobClient CreateBobClient(RoundState roundState)
	{
		var arenaRequestHandler = new WabiSabiHttpApiClient(HttpClientFactory.NewHttpClientWithCircuitPerRequest());

		return new BobClient(
			roundState.Id,
			new(
				roundState.CreateAmountCredentialClient(SecureRandom),
				roundState.CreateVsizeCredentialClient(SecureRandom),
				arenaRequestHandler));
	}

	private bool SanityCheck(IEnumerable<TxOut> expectedOutputs, Transaction unsignedCoinJoinTransaction)
	{
		var coinJoinOutputs = unsignedCoinJoinTransaction.Outputs.Select(o => (o.Value, o.ScriptPubKey));
		IEnumerable<(Money TotalAmount, Script Key)>? expectedOutputTuples = expectedOutputs
			.GroupBy(x => x.ScriptPubKey)
			.Select(o => (o.Select(x => x.Value).Sum(), o.Key));

		return coinJoinOutputs.IsSuperSetOf(expectedOutputTuples);
	}

	private async Task SignTransactionAsync(IEnumerable<AliceClient> aliceClients, Transaction unsignedCoinJoinTransaction, RoundState roundState, CancellationToken cancellationToken)
	{
		async Task<AliceClient?> SignTransactionAsync(AliceClient aliceClient, CancellationToken cancellationToken)
		{
			try
			{
				await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, KeyChain, cancellationToken).ConfigureAwait(false);
				return aliceClient;
			}
			catch (Exception e)
			{
				Logger.LogWarning($"Round ({aliceClient.RoundId}), Alice ({aliceClient.AliceId}): Could not sign, reason:'{e}'.");
				return default;
			}
		}

		// Gets the list of scheduled dates/time in the remaining available time frame when each alice has to sign.
		var transactionSigningTimeFrame = roundState.TransactionSigningTimeout - RoundStatusUpdater.Period;
		Logger.LogDebug($"Round ({roundState.Id}): Signing phase started, it will end in {transactionSigningTimeFrame:hh\\:mm\\:ss}.");

		var scheduledDates = transactionSigningTimeFrame.SamplePoisson(aliceClients.Count());

		// Creates scheduled tasks (tasks that wait until the specified date/time and then perform the real registration)
		var signingRequests = aliceClients.Zip(
			scheduledDates,
			async (alice, date) =>
			{
				var delay = date - DateTimeOffset.UtcNow;
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
				return await SignTransactionAsync(alice, cancellationToken).ConfigureAwait(false);
			}).ToImmutableArray();

		await Task.WhenAll(signingRequests).ConfigureAwait(false);
	}

	private async Task ReadyToSignAsync(IEnumerable<AliceClient> aliceClients, CancellationToken cancellationToken)
	{
		async Task ReadyToSignTask(AliceClient aliceClient)
		{
			await aliceClient.ReadyToSignAsync(cancellationToken).ConfigureAwait(false);
		}

		var readyRequests = aliceClients.Select(ReadyToSignTask);

		await Task.WhenAll(readyRequests).ConfigureAwait(false);
	}

	private void LogCoinJoinSummary(ImmutableArray<AliceClient> registeredAliceClients, IEnumerable<TxOut> myOutputs, Transaction unsignedCoinJoinTransaction, RoundState roundState)
	{
		var feeRate = roundState.FeeRate;

		var totalEffectiveInputAmount = Money.Satoshis(registeredAliceClients.Sum(a => a.EffectiveValue));
		var totalEffectiveOutputAmount = Money.Satoshis(myOutputs.Sum(a => a.Value - feeRate.GetFee(a.ScriptPubKey.EstimateOutputVsize())));
		var effectiveDifference = totalEffectiveOutputAmount - totalEffectiveInputAmount;

		var totalInputAmount = Money.Satoshis(registeredAliceClients.Sum(a => a.SmartCoin.Amount));
		var totalOutputAmount = Money.Satoshis(myOutputs.Sum(a => a.Value));
		var totalDifference = Money.Satoshis(totalOutputAmount - totalInputAmount);

		var inputNetworkFee = Money.Satoshis(registeredAliceClients.Sum(alice => feeRate.GetFee(alice.SmartCoin.Coin.ScriptPubKey.EstimateInputVsize())));
		var outputNetworkFee = Money.Satoshis(myOutputs.Sum(output => feeRate.GetFee(output.ScriptPubKey.EstimateOutputVsize())));
		var totalNetworkFee = inputNetworkFee + outputNetworkFee;
		var totalCoordinationFee = Money.Satoshis(registeredAliceClients.Where(a => a.IsPayingZeroCoordinationFee).Sum(a => roundState.CoordinationFeeRate.GetFee(a.SmartCoin.Amount)));

		string[] summary = new string[] {
		$"Round ({roundState.Id}):",
		$"\tInput total: {totalInputAmount.ToString(true)} Eff: {totalEffectiveInputAmount.ToString(true)} NetwFee: {inputNetworkFee.ToString(true)} CoordF: {totalCoordinationFee.ToString(true)}",
		$"\tOutpu total: {totalOutputAmount.ToString(true)} Eff: {totalEffectiveOutputAmount.ToString(true)} NetwFee: {outputNetworkFee.ToString(true)}",
		$"\tTotal diff : {totalDifference.ToString(true)}",
		$"\tEffec diff : {effectiveDifference.ToString(true)}",
		$"\tTotal fee  : {totalNetworkFee.ToString(true)}"
		};

		Logger.LogDebug(string.Join(Environment.NewLine, summary));
	}

	internal static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> coins, MultipartyTransactionParameters parameters, bool consolidationMode, int minAnonScoreTarget, WasabiRandom rnd)
	{
		var filteredCoins = coins
			.Where(x => parameters.AllowedInputAmounts.Contains(x.Amount))
			.Where(x => parameters.AllowedInputTypes.Any(t => x.ScriptPubKey.IsScriptType(t)))
			.ToShuffled() // Preshuffle before ordering.
			.OrderBy(x => x.HdPubKey.AnonymitySet)
			.ThenByDescending(y => y.Amount)
			.ToArray();

		// How many inputs do we want to provide to the mix?
		int inputCount = consolidationMode ? MaxInputsRegistrableByWallet : GetInputTarget(filteredCoins.Length, rnd);

		// Select a group of coins those are close to each other by Anonimity Score.
		List<IEnumerable<SmartCoin>> groups = new();

		// I can take more coins those are already reached the minimum privacy threshold.
		int nonPrivateCoinCount = filteredCoins.Count(x => x.HdPubKey.AnonymitySet < minAnonScoreTarget);
		for (int i = 0; i < nonPrivateCoinCount; i++)
		{
			// Make sure the group can at least register an output even after paying fees.
			var group = filteredCoins.Skip(i).Take(inputCount);

			if (group.Count() < Math.Min(filteredCoins.Length, inputCount))
			{
				break;
			}

			var inSum = group.Sum(x => x.EffectiveValue(parameters.FeeRate, parameters.CoordinationFeeRate));
			var outFee = parameters.FeeRate.GetFee(Constants.P2wpkhOutputSizeInBytes);
			if (inSum >= outFee + parameters.AllowedOutputAmounts.Min)
			{
				groups.Add(group);
			}
		}

		// Calculate the anonScore cost of input consolidation.
		List<(long Cost, IEnumerable<SmartCoin> Group)> anonScoreCosts = new();
		foreach (var group in groups)
		{
			var smallestAnon = group.Min(x => x.HdPubKey.AnonymitySet);
			var cost = 0L;
			foreach (var coin in group.Where(c => c.HdPubKey.AnonymitySet != smallestAnon))
			{
				cost += (coin.Amount.Satoshi * coin.HdPubKey.AnonymitySet) - (coin.Amount.Satoshi * smallestAnon);
			}

			anonScoreCosts.Add((cost, group));
		}

		if (anonScoreCosts.Count == 0)
		{
			return ImmutableList<SmartCoin>.Empty;
		}

		var bestCost = anonScoreCosts.Min(g => g.Cost);
		var bestgroups = anonScoreCosts.Where(x => x.Cost == bestCost).Select(x => x.Group);

		// Select the group where the less coins coming from the same tx.
		var bestgroup = bestgroups
			.Select(group =>
				(Reps: group.GroupBy(x => x.TransactionId).Sum(coinsInTxGroup => coinsInTxGroup.Count() - 1),
				Group: group))
			.ToShuffled()
			.MinBy(i => i.Reps).Group;

		return bestgroup.ToShuffled().ToImmutableList();
	}

	public bool IsRoundEconomic(FeeRate roundFeeRate)
	{
		if (FeeRateMedianTimeFrame == default)
		{
			return true;
		}

		if (RoundStatusUpdater.CoinJoinFeeRateMedians.TryGetValue(FeeRateMedianTimeFrame, out var medianFeeRate))
		{
			// 0.5 satoshi difference is allowable, to avoid rounding errors.
			return roundFeeRate.SatoshiPerByte <= medianFeeRate.SatoshiPerByte + 0.5m;
		}

		throw new InvalidOperationException($"Could not find median feeRate for timeframe: {FeeRateMedianTimeFrame}.");
	}

	/// <summary>
	/// Calculates how many inputs are desirable to be registered
	/// based on roughly the total number of coins in a wallet.
	/// Note: random biasing is applied.
	/// </summary>
	/// <returns>Desired input count.</returns>
	private static int GetInputTarget(int utxoCount, WasabiRandom rnd)
	{
		int targetInputCount;
		if (utxoCount < 35)
		{
			targetInputCount = 1;
		}
		else if (utxoCount > 150)
		{
			targetInputCount = MaxInputsRegistrableByWallet;
		}
		else
		{
			var min = 2;
			var max = MaxInputsRegistrableByWallet - 1;

			var percent = (double)(utxoCount - 35) / (150 - 35);
			targetInputCount = (int)Math.Round((max - min) * percent + min);
		}

		var distance = new Dictionary<int, int>();
		for (int i = 1; i <= MaxInputsRegistrableByWallet; i++)
		{
			distance.TryAdd(i, Math.Abs(i - targetInputCount));
		}

		foreach (var best in distance.OrderBy(x => x.Value))
		{
			if (rnd.GetInt(0, 10) < 5)
			{
				return best.Key;
			}
		}

		return targetInputCount;
	}
}
