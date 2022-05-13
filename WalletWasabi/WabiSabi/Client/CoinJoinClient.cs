using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinClient
{
	private const int MaxInputsRegistrableByWallet = 10; // how many
	private static readonly Money MinimumOutputAmountSanity = Money.Coins(0.0001m); // ignore rounds with too big minimum denominations
	private static readonly TimeSpan ExtraPhaseTimeoutMargin = TimeSpan.FromMinutes(1);

	// Maximum delay when spreading the requests in time, except input registration requests which
	// timings only depends on the input-reg timeout.
	// This is a maximum cap the delay can be smaller if the remaining time is less.
	private static readonly TimeSpan MaximumRequestDelay = TimeSpan.FromSeconds(10);

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

	public event EventHandler<CoinJoinProgressEventArgs>? CoinJoinClientProgress;

	private SecureRandom SecureRandom { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	private IKeyChain KeyChain { get; }
	private IDestinationProvider DestinationProvider { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	public int MinAnonScoreTarget { get; }
	private TimeSpan DoNotRegisterInLastMinuteTimeLimit { get; }

	public bool ConsolidationMode { get; private set; }
	private TimeSpan FeeRateMedianTimeFrame { get; }

	private async Task<RoundState> WaitForRoundAsync(uint256 excludeRound, CancellationToken token)
	{
		CoinJoinClientProgress.SafeInvoke(this, new WaitingForRound());
		return await RoundStatusUpdater
			.CreateRoundAwaiter(
				roundState =>
					roundState.InputRegistrationEnd - DateTimeOffset.UtcNow > DoNotRegisterInLastMinuteTimeLimit
					&& roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min < MinimumOutputAmountSanity
					&& roundState.Phase == Phase.InputRegistration
					&& roundState.BlameOf == uint256.Zero
					&& IsRoundEconomic(roundState.CoinjoinState.Parameters.MiningFeeRate)
					&& roundState.Id != excludeRound,
				token)
			.ConfigureAwait(false);
	}

	private async Task<RoundState> WaitForBlameRoundAsync(uint256 blameRoundId, CancellationToken token)
	{
		var timeout = TimeSpan.FromMinutes(5);
		CoinJoinClientProgress.SafeInvoke(this, new WaitingForBlameRound(DateTimeOffset.UtcNow + timeout));

		using CancellationTokenSource waitForBlameRoundCts = new(timeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(waitForBlameRoundCts.Token, token);

		var roundState = await RoundStatusUpdater
				.CreateRoundAwaiter(
					roundState => roundState.BlameOf == blameRoundId,
					linkedCts.Token)
				.ConfigureAwait(false);

		if (roundState.Phase is not Phase.InputRegistration)
		{
			throw new InvalidOperationException($"Blame Round ({roundState.Id}): Abandoning: the round is not in Input Registration but in '{roundState.Phase}'.");
		}

		if (roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min >= MinimumOutputAmountSanity)
		{
			throw new InvalidOperationException($"Blame Round ({roundState.Id}): Abandoning: the minimum output amount is too high.");
		}

		if (!IsRoundEconomic(roundState.CoinjoinState.Parameters.MiningFeeRate))
		{
			throw new InvalidOperationException($"Blame Round ({roundState.Id}): Abandoning: the round is not economic.");
		}

		return roundState;
	}

	public async Task<CoinJoinResult> StartCoinJoinAsync(IEnumerable<SmartCoin> coinCandidates, CancellationToken cancellationToken)
	{
		var tryLimit = 6;

		RoundState? currentRoundState;
		uint256 excludeRound = uint256.Zero;
		ImmutableList<SmartCoin> coins;

		do
		{
			currentRoundState = await WaitForRoundAsync(excludeRound, cancellationToken).ConfigureAwait(false);
			RoundParameters roundParameteers = currentRoundState.CoinjoinState.Parameters;
			coins = SelectCoinsForRound(coinCandidates, roundParameteers, ConsolidationMode, MinAnonScoreTarget, SecureRandom);

			if (roundParameteers.MaxSuggestedAmount != default && coins.Any(c => c.Amount > roundParameteers.MaxSuggestedAmount))
			{
				excludeRound = currentRoundState.Id;
				currentRoundState.LogInfo($"Skipping the round for more optimal mixing. Max suggested amount is '{roundParameteers.MaxSuggestedAmount}' BTC, biggest coin amount is: '{coins.Select(c => c.Amount).Max()}' BTC.");

				continue;
			}

			break;
		}
		while (!cancellationToken.IsCancellationRequested);

		if (coins.IsEmpty)
		{
			throw new InvalidOperationException($"No coin was selected from '{coinCandidates.Count()}' number of coins.");
		}

		for (var tries = 0; tries < tryLimit; tries++)
		{
			CoinJoinResult result = await StartRoundAsync(coins, currentRoundState, cancellationToken).ConfigureAwait(false);
			if (!result.GoForBlameRound)
			{
				return result;
			}

			// Only use successfully registered coins in the blame round.
			coins = result.RegisteredCoins;

			currentRoundState.LogInfo($"Waiting for the blame round.");
			currentRoundState = await WaitForBlameRoundAsync(currentRoundState.Id, cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("Blame rounds were not successful.");
	}

	public async Task<CoinJoinResult> StartRoundAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		var roundId = roundState.Id;

		ImmutableArray<(AliceClient AliceClient, PersonCircuit PersonCircuit)> registeredAliceClientAndCircuits = ImmutableArray<(AliceClient, PersonCircuit)>.Empty;

		// Because of the nature of the protocol, the input registration and the connection confirmation phases are done subsequently thus they have a merged timeout.
		var timeUntilOutputReg = (roundState.InputRegistrationEnd - DateTimeOffset.Now) + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;

		try
		{
			try
			{
				using CancellationTokenSource timeUntilOutputRegCts = new(timeUntilOutputReg + ExtraPhaseTimeoutMargin);
				using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeUntilOutputRegCts.Token);

				registeredAliceClientAndCircuits = await ProceedWithInputRegAndConfirmAsync(smartCoins, roundState, linkedCts.Token).ConfigureAwait(false);
			}
			catch (UnexpectedRoundPhaseException ex)
			{
				roundState.LogInfo($"Registration phase ended by the coordinator: '{ex.Message}'.");
				return new CoinJoinResult(false);
			}

			if (!registeredAliceClientAndCircuits.Any())
			{
				roundState.LogInfo("There are no available Alices to participate with.");
				return new CoinJoinResult(false);
			}

			var registeredAliceClients = registeredAliceClientAndCircuits.Select(x => x.AliceClient).ToImmutableArray();

			var outputTxOuts = await ProceedWithOutputRegistrationPhaseAsync(roundId, registeredAliceClients, cancellationToken).ConfigureAwait(false);

			var unsignedCoinJoin = await ProceedWithSigningStateAsync(roundId, registeredAliceClients, outputTxOuts, cancellationToken).ConfigureAwait(false);

			var finalRoundState = await RoundStatusUpdater.CreateRoundAwaiter(s => s.Id == roundId && s.Phase == Phase.Ended, cancellationToken).ConfigureAwait(false);

			CoinJoinClientProgress.SafeInvoke(this, new RoundEnded(finalRoundState));

			var wasTxBroadcast = finalRoundState.WasTransactionBroadcast
				? $"'{finalRoundState.WasTransactionBroadcast}', Coinjoin TxId: ({unsignedCoinJoin.GetHash()})"
				: $"'{finalRoundState.WasTransactionBroadcast}'";
			roundState.LogDebug($"Ended - WasTransactionBroadcast: {wasTxBroadcast}.");

			LogCoinJoinSummary(registeredAliceClients, outputTxOuts, unsignedCoinJoin, finalRoundState);

			return new CoinJoinResult(
				GoForBlameRound: !finalRoundState.WasTransactionBroadcast,
				SuccessfulBroadcast: finalRoundState.WasTransactionBroadcast,
				RegisteredCoins: registeredAliceClients.Select(a => a.SmartCoin).ToImmutableList(),
				RegisteredOutputs: outputTxOuts.Select(o => o.ScriptPubKey).ToImmutableList());
		}
		finally
		{
			foreach (var aliceClientAndCircuit in registeredAliceClientAndCircuits)
			{
				aliceClientAndCircuit.AliceClient.Finish();
				aliceClientAndCircuit.PersonCircuit.Dispose();
			}
			CoinJoinClientProgress.SafeInvoke(this, new LeavingCriticalPhase());
		}
	}

	private async Task<ImmutableArray<(AliceClient AliceClient, PersonCircuit PersonCircuit)>> CreateRegisterAndConfirmCoinsAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		bool alreadyEnteredToCritical = false;
		object alreadyEnteredToCriticalLock = new();

		void ReportProgressOnce()
		{
			if (alreadyEnteredToCritical)
			{
				return;
			}

			// Helper to not invoke event inside the lock.
			bool reportProgress = false;
			lock (alreadyEnteredToCriticalLock)
			{
				if (alreadyEnteredToCritical)
				{
					return;
				}
				reportProgress = true;
				alreadyEnteredToCritical = true;
			}
			if (reportProgress)
			{
				CoinJoinClientProgress.SafeInvoke(this, new EnteringCriticalPhase());
			}
		}

		async Task<(AliceClient? AliceClient, PersonCircuit? PersonCircuit)> RegisterInputAsync(SmartCoin coin, CancellationToken cancellationToken)
		{
			PersonCircuit? personCircuit = null;
			try
			{
				personCircuit = HttpClientFactory.NewHttpClientWithPersonCircuit(out Tor.Http.IHttpClient httpClient);

				// Alice client requests are inherently linkable to each other, so the circuit can be reused
				var arenaRequestHandler = new WabiSabiHttpApiClient(httpClient);

				var aliceArenaClient = new ArenaClient(
					roundState.CreateAmountCredentialClient(SecureRandom),
					roundState.CreateVsizeCredentialClient(SecureRandom),
					arenaRequestHandler);

				var aliceClient = await AliceClient.CreateRegisterAndConfirmInputAsync(roundState, aliceArenaClient, coin, KeyChain, RoundStatusUpdater, cancellationToken).ConfigureAwait(false);

				// Right after the first real-cred confirmation happened we entered into critical phase.
				ReportProgressOnce();

				return (aliceClient, personCircuit);
			}
			catch (HttpRequestException)
			{
				personCircuit?.Dispose();
				return (null, null);
			}
			catch (Exception)
			{
				personCircuit?.Dispose();
				throw;
			}
		}

		// Gets the list of scheduled dates/time in the remaining available time frame when each alice has to be registered.
		var remainingTimeForRegistration = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow;

		roundState.LogDebug($"Input registration started - it will end in: {remainingTimeForRegistration:hh\\:mm\\:ss}.");

		var scheduledDates = GetScheduledDates(smartCoins.Count(), roundState.InputRegistrationEnd);

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
			})
			.ToImmutableArray();

		await Task.WhenAll(aliceClients).ConfigureAwait(false);

		return aliceClients
			.Select(x => x.Result)
			.Where(r => r.AliceClient is not null && r.PersonCircuit is not null)
			.Select(r => (r.AliceClient!, r.PersonCircuit!))
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

	private async Task SignTransactionAsync(IEnumerable<AliceClient> aliceClients, Transaction unsignedCoinJoinTransaction, DateTimeOffset signingEndTime, CancellationToken cancellationToken)
	{
		var scheduledDates = GetScheduledDates(aliceClients.Count(), signingEndTime, MaximumRequestDelay);

		var tasks = Enumerable.Zip(
			aliceClients,
			scheduledDates,
			async (aliceClient, scheduledDate) =>
			{
				var delay = scheduledDate - DateTimeOffset.UtcNow;
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
				await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, KeyChain, cancellationToken).ConfigureAwait(false);
			})
			.ToImmutableArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private async Task ReadyToSignAsync(IEnumerable<AliceClient> aliceClients, DateTimeOffset readyToSignEndTime, CancellationToken cancellationToken)
	{
		var scheduledDates = GetScheduledDates(aliceClients.Count(), readyToSignEndTime, MaximumRequestDelay);

		var tasks = Enumerable.Zip(
			aliceClients,
			scheduledDates,
			async (aliceClient, scheduledDate) =>
			{
				var delay = scheduledDate - DateTimeOffset.UtcNow;
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
				await aliceClient.ReadyToSignAsync(cancellationToken).ConfigureAwait(false);
			})
			.ToImmutableArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private ImmutableList<DateTimeOffset> GetScheduledDates(int howMany, DateTimeOffset endTime)
	{
		return GetScheduledDates(howMany, endTime, TimeSpan.MaxValue);
	}

	internal virtual ImmutableList<DateTimeOffset> GetScheduledDates(int howMany, DateTimeOffset endTime, TimeSpan maximumRequestDelay)
	{
		var remainingTime = endTime - DateTimeOffset.UtcNow;

		if (remainingTime > maximumRequestDelay)
		{
			remainingTime = maximumRequestDelay;
		}

		return remainingTime.SamplePoisson(howMany);
	}

	private void LogCoinJoinSummary(ImmutableArray<AliceClient> registeredAliceClients, IEnumerable<TxOut> myOutputs, Transaction unsignedCoinJoinTransaction, RoundState roundState)
	{
		RoundParameters roundParameters = roundState.CoinjoinState.Parameters;
		FeeRate feeRate = roundParameters.MiningFeeRate;

		var totalEffectiveInputAmount = Money.Satoshis(registeredAliceClients.Sum(a => a.EffectiveValue));
		var totalEffectiveOutputAmount = Money.Satoshis(myOutputs.Sum(a => a.Value - feeRate.GetFee(a.ScriptPubKey.EstimateOutputVsize())));
		var effectiveDifference = totalEffectiveInputAmount - totalEffectiveOutputAmount;

		var totalInputAmount = Money.Satoshis(registeredAliceClients.Sum(a => a.SmartCoin.Amount));
		var totalOutputAmount = Money.Satoshis(myOutputs.Sum(a => a.Value));
		var totalDifference = Money.Satoshis(totalInputAmount - totalOutputAmount);

		var inputNetworkFee = Money.Satoshis(registeredAliceClients.Sum(alice => feeRate.GetFee(alice.SmartCoin.Coin.ScriptPubKey.EstimateInputVsize())));
		var outputNetworkFee = Money.Satoshis(myOutputs.Sum(output => feeRate.GetFee(output.ScriptPubKey.EstimateOutputVsize())));
		var totalNetworkFee = inputNetworkFee + outputNetworkFee;
		var totalCoordinationFee = Money.Satoshis(registeredAliceClients.Where(a => a.IsPayingZeroCoordinationFee).Sum(a => roundParameters.CoordinationFeeRate.GetFee(a.SmartCoin.Amount)));

		string[] summary = new string[]
		{
			$"",
			$"\tInput total: {totalInputAmount.ToString(true, false)} Eff: {totalEffectiveInputAmount.ToString(true, false)} NetwFee: {inputNetworkFee.ToString(true, false)} CoordFee: {totalCoordinationFee.ToString(true)}",
			$"\tOutpu total: {totalOutputAmount.ToString(true, false)} Eff: {totalEffectiveOutputAmount.ToString(true, false)} NetwFee: {outputNetworkFee.ToString(true, false)}",
			$"\tTotal diff : {totalDifference.ToString(true, false)}",
			$"\tEffec diff : {effectiveDifference.ToString(true, false)}",
			$"\tTotal fee  : {totalNetworkFee.ToString(true, false)}"
		};

		roundState.LogDebug(string.Join(Environment.NewLine, summary));
	}

	internal static ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> coins, RoundParameters parameters, bool consolidationMode, int minAnonScoreTarget, WasabiRandom rnd)
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

		var nonPrivateFilteredCoins = filteredCoins
			.Where(x => x.HdPubKey.AnonymitySet < minAnonScoreTarget)
			.ToArray();

		// Select a group of coins those are close to each other by Anonimity Score.
		Dictionary<int, IEnumerable<SmartCoin>> groups = new();

		// I can take more coins those are already reached the minimum privacy threshold.
		for (int i = 0; i < nonPrivateFilteredCoins.Length; i++)
		{
			// Make sure the group can at least register an output even after paying fees.
			var group = filteredCoins.Skip(i).Take(inputCount);

			if (group.Count() < Math.Min(filteredCoins.Length, inputCount))
			{
				break;
			}

			TryAddGroup(groups, group, parameters);
		}

		// We can potentially add a lot more groups to improve results.
		var sw1 = Stopwatch.StartNew();
		foreach (var coin in nonPrivateFilteredCoins.OrderByDescending(x => x.Amount))
		{
			var sw2 = Stopwatch.StartNew();
			foreach (var group in filteredCoins.Except(new[] { coin }).CombinationsWithoutRepetition(inputCount - 1))
			{
				TryAddGroup(groups, group.Concat(new[] { coin }), parameters);

				if (sw2.Elapsed > TimeSpan.FromSeconds(1))
				{
					break;
				}
			}

			sw2.Reset();

			if (sw1.Elapsed > TimeSpan.FromSeconds(10))
			{
				break;
			}
		}

		// Calculate the anonScore cost of input consolidation.
		List<(long Cost, IEnumerable<SmartCoin> Group)> anonScoreCosts = new();
		foreach (var group in groups)
		{
			var smallestAnon = group.Value.Min(x => x.HdPubKey.AnonymitySet);
			var cost = 0L;
			foreach (var coin in group.Value.Where(c => c.HdPubKey.AnonymitySet != smallestAnon))
			{
				cost += (coin.Amount.Satoshi * coin.HdPubKey.AnonymitySet) - (coin.Amount.Satoshi * smallestAnon);
			}

			anonScoreCosts.Add((cost, group.Value));
		}

		if (anonScoreCosts.Count == 0)
		{
			return ImmutableList<SmartCoin>.Empty;
		}

		var bestCost = anonScoreCosts.Min(g => g.Cost);
		var bestCostGroups = anonScoreCosts.Where(x => x.Cost == bestCost).Select(x => x.Group);

		// Select the group where the less coins coming from the same tx.
		var bestRep = bestCostGroups.Select(x => GetReps(x)).Min(x => x);
		var bestRepGroups = bestCostGroups.Where(x => GetReps(x) == bestRep);

		var bestgroup = bestRepGroups
			.ToShuffled()
			.MaxBy(x => x.Sum(y => y.Amount))!;

		return bestgroup.ToShuffled().ToImmutableList();
	}

	private static int GetReps(IEnumerable<SmartCoin> group)
		=> group.GroupBy(x => x.TransactionId).Sum(coinsInTxGroup => coinsInTxGroup.Count() - 1);

	private static bool TryAddGroup(IDictionary<int, IEnumerable<SmartCoin>> groups, IEnumerable<SmartCoin> group, RoundParameters parameters)
	{
		var inSum = group.Sum(x => x.EffectiveValue(parameters.MiningFeeRate, parameters.CoordinationFeeRate));
		var outFee = parameters.MiningFeeRate.GetFee(Constants.P2wpkhOutputVirtualSize);
		if (inSum >= outFee + parameters.AllowedOutputAmounts.Min)
		{
			var k = HashCode.Combine(group.OrderBy(x => x.TransactionId).ThenBy(x => x.Index));

			return CollectionExtensions.TryAdd(groups, k, group);
		}

		return false;
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

		throw new InvalidOperationException($"Could not find median fee rate for time frame: {FeeRateMedianTimeFrame}.");
	}

	/// <summary>
	/// Calculates how many inputs are desirable to be registered
	/// based on roughly the total number of coins in a wallet.
	/// Note: random biasing is applied.
	/// </summary>
	/// <returns>Desired input count.</returns>
	private static int GetInputTarget(int utxoCount, WasabiRandom rnd)
	{
		var minUtxoCountTarget = 21;
		var maxUtxoCountTarget = 100;

		int targetInputCount;
		if (utxoCount < minUtxoCountTarget)
		{
			targetInputCount = 1;
		}
		else if (utxoCount > maxUtxoCountTarget)
		{
			targetInputCount = MaxInputsRegistrableByWallet;
		}
		else
		{
			var min = 2;
			var max = MaxInputsRegistrableByWallet - 1;

			var percent = (double)(utxoCount - minUtxoCountTarget) / (maxUtxoCountTarget - minUtxoCountTarget);
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

	private async Task<IEnumerable<TxOut>> ProceedWithOutputRegistrationPhaseAsync(uint256 roundId, ImmutableArray<AliceClient> registeredAliceClients, CancellationToken cancellationToken)
	{
		// Waiting for OutputRegistration phase, all the Alices confirmed their connections, so the list of the inputs will be complete.
		var roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundId, Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
		var roundParameters = roundState.CoinjoinState.Parameters;
		var remainingTime = roundParameters.OutputRegistrationTimeout - RoundStatusUpdater.Period;
		var now = DateTimeOffset.UtcNow;
		var outputRegistrationPhaseEndTime = now + remainingTime;

		// Splitting the ramaining time.
		// Both operations are done under output registration phase, so we have to do the random timing taking that into account.
		var outputRegistrationEndTime = now + (remainingTime * 0.8); // 80% of the time.
		var readyToSignEndTime = outputRegistrationEndTime + remainingTime * 0.2; // 20% of the time.

		CoinJoinClientProgress.SafeInvoke(this, new EnteringOutputRegistrationPhase(roundState, outputRegistrationPhaseEndTime));

		using CancellationTokenSource phaseTimeoutCts = new(remainingTime + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, phaseTimeoutCts.Token);

		var registeredCoins = registeredAliceClients.Select(x => x.SmartCoin.Coin);
		var inputEffectiveValuesAndSizes = registeredAliceClients.Select(x => (x.EffectiveValue, x.SmartCoin.ScriptPubKey.EstimateInputVsize()));
		var availableVsize = registeredAliceClients.SelectMany(x => x.IssuedVsizeCredentials).Sum(x => x.Value);

		// Calculate outputs values
		var constructionState = roundState.Assert<ConstructionState>();

		AmountDecomposer amountDecomposer = new(roundParameters.MiningFeeRate, roundParameters.AllowedOutputAmounts, Constants.P2wpkhOutputVirtualSize, Constants.P2wpkhInputVirtualSize, (int)availableVsize);
		var theirCoins = constructionState.Inputs.Where(x => !registeredCoins.Any(y => x.Outpoint == y.Outpoint));
		var registeredCoinEffectiveValues = registeredAliceClients.Select(x => x.EffectiveValue);
		var theirCoinEffectiveValues = theirCoins.Select(x => x.EffectiveValue(roundParameters.MiningFeeRate, roundParameters.CoordinationFeeRate));
		var outputValues = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues);

		// Get as many destinations as outputs we need.
		var destinations = DestinationProvider.GetNextDestinations(outputValues.Count()).ToArray();
		var outputTxOuts = outputValues.Zip(destinations, (amount, destination) => new TxOut(amount, destination.ScriptPubKey));

		DependencyGraph dependencyGraph = DependencyGraph.ResolveCredentialDependencies(inputEffectiveValuesAndSizes, outputTxOuts, roundParameters.MiningFeeRate, roundParameters.CoordinationFeeRate, roundParameters.MaxVsizeAllocationPerAlice);
		DependencyGraphTaskScheduler scheduler = new(dependencyGraph);

		// Re-issuances.
		var bobClient = CreateBobClient(roundState);
		roundState.LogInfo("Starting reissuances.");
		var combinedToken = linkedCts.Token;
		await scheduler.StartReissuancesAsync(registeredAliceClients, bobClient, combinedToken).ConfigureAwait(false);

		// Output registration.
		roundState.LogDebug($"Output registration started - it will end in: {outputRegistrationEndTime - DateTimeOffset.UtcNow:hh\\:mm\\:ss}.");

		var outputRegistrationScheduledDates = GetScheduledDates(outputTxOuts.Count(), outputRegistrationEndTime, MaximumRequestDelay);
		await scheduler.StartOutputRegistrationsAsync(outputTxOuts, bobClient, KeyChain, outputRegistrationScheduledDates, combinedToken).ConfigureAwait(false);
		roundState.LogDebug($"Outputs({outputTxOuts.Count()}) were registered.");

		// ReadyToSign.
		roundState.LogDebug($"ReadyToSign phase started - it will end in: {readyToSignEndTime - DateTimeOffset.UtcNow:hh\\:mm\\:ss}.");
		await ReadyToSignAsync(registeredAliceClients, readyToSignEndTime, combinedToken).ConfigureAwait(false);
		roundState.LogDebug($"Alices({registeredAliceClients.Length}) are ready to sign.");
		return outputTxOuts;
	}

	private async Task<Transaction> ProceedWithSigningStateAsync(uint256 roundId, ImmutableArray<AliceClient> registeredAliceClients, IEnumerable<TxOut> outputTxOuts, CancellationToken cancellationToken)
	{
		// Signing.
		var roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundId, Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
		var remainingTime = roundState.CoinjoinState.Parameters.TransactionSigningTimeout - RoundStatusUpdater.Period;
		var signingStateEndTime = DateTimeOffset.UtcNow + remainingTime;

		CoinJoinClientProgress.SafeInvoke(this, new EnteringSigningPhase(roundState, signingStateEndTime));

		using CancellationTokenSource phaseTimeoutCts = new(remainingTime + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, phaseTimeoutCts.Token);

		roundState.LogDebug($"Transaction signing phase started - it will end in: {signingStateEndTime - DateTimeOffset.UtcNow:hh\\:mm\\:ss}.");

		var signingState = roundState.Assert<SigningState>();
		var unsignedCoinJoin = signingState.CreateUnsignedTransaction();

		// Sanity check.
		if (!SanityCheck(outputTxOuts, unsignedCoinJoin))
		{
			string round = roundState.BlameOf == 0 ? "Round" : "Blame Round";

			throw new InvalidOperationException($"{round} ({roundState.Id}): My output is missing.");
		}

		// Send signature.
		var combinedToken = linkedCts.Token;
		await SignTransactionAsync(registeredAliceClients, unsignedCoinJoin, signingStateEndTime, combinedToken).ConfigureAwait(false);
		roundState.LogDebug($"Alices({registeredAliceClients.Length}) have signed the coinjoin tx.");

		return unsignedCoinJoin;
	}

	private async Task<ImmutableArray<(AliceClient, PersonCircuit)>> ProceedWithInputRegAndConfirmAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		var remainingTime = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow;

		using CancellationTokenSource phaseTimeoutCts = new(remainingTime + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, phaseTimeoutCts.Token);
		var combinedToken = linkedCts.Token;

		CoinJoinClientProgress.SafeInvoke(this, new EnteringInputRegistrationPhase(roundState, roundState.InputRegistrationEnd));

		// Register coins.
		var result = await CreateRegisterAndConfirmCoinsAsync(smartCoins, roundState, combinedToken).ConfigureAwait(false);

		if (!RoundStatusUpdater.TryGetRoundState(roundState.Id, out var newRoundState))
		{
			throw new InvalidOperationException($"Round '{roundState.Id}' is missing.");
		}

		// Be aware: at this point we are already in connection confirmation and all the coins got their first confirmation, so this is not exactly the starting time of the phase.
		var estimatedRemainingFromConnectionConfirmation = DateTimeOffset.UtcNow + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;
		CoinJoinClientProgress.SafeInvoke(this, new EnteringConnectionConfirmationPhase(newRoundState, estimatedRemainingFromConnectionConfirmation));

		return result;
	}
}
