using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend.Models;
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
	private const int MaxWeightedAnonLoss = 3;
	private static readonly Money MinimumOutputAmountSanity = Money.Coins(0.0001m); // ignore rounds with too big minimum denominations
	private static readonly TimeSpan ExtraPhaseTimeoutMargin = TimeSpan.FromMinutes(1);

	// Maximum delay when spreading the requests in time, except input registration requests which
	// timings only depends on the input-reg timeout.
	// This is a maximum cap the delay can be smaller if the remaining time is less.
	private static readonly TimeSpan MaximumRequestDelay = TimeSpan.FromSeconds(10);

	/// <param name="anonScoreTarget">Coins those have reached anonymity target, but still can be mixed if desired.</param>
	/// <param name="consolidationMode">If true, then aggressively try to consolidate as many coins as it can.</param>
	public CoinJoinClient(
		IWasabiHttpClientFactory httpClientFactory,
		IKeyChain keyChain,
		IDestinationProvider destinationProvider,
		RoundStateUpdater roundStatusUpdater,
		string coordinatorIdentifier,
		int anonScoreTarget = int.MaxValue,
		bool consolidationMode = false,
		bool redCoinIsolation = false,
		TimeSpan feeRateMedianTimeFrame = default,
		TimeSpan doNotRegisterInLastMinuteTimeLimit = default,
		Money? liquidityClue = null)
	{
		HttpClientFactory = httpClientFactory;
		KeyChain = keyChain;
		DestinationProvider = destinationProvider;
		RoundStatusUpdater = roundStatusUpdater;
		AnonScoreTarget = anonScoreTarget;
		CoordinatorIdentifier = coordinatorIdentifier;
		ConsolidationMode = consolidationMode;
		RedCoinIsolation = redCoinIsolation;
		FeeRateMedianTimeFrame = feeRateMedianTimeFrame;
		SecureRandom = new SecureRandom();
		DoNotRegisterInLastMinuteTimeLimit = doNotRegisterInLastMinuteTimeLimit;
		lock (LiquidityClueLock)
		{
			LiquidityClue ??= liquidityClue;
		}
	}

	public event EventHandler<CoinJoinProgressEventArgs>? CoinJoinClientProgress;

	private SecureRandom SecureRandom { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	private IKeyChain KeyChain { get; }
	private IDestinationProvider DestinationProvider { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	public string CoordinatorIdentifier { get; }
	public int AnonScoreTarget { get; }
	private TimeSpan DoNotRegisterInLastMinuteTimeLimit { get; }

	public bool ConsolidationMode { get; private set; }
	public bool RedCoinIsolation { get; }
	private TimeSpan FeeRateMedianTimeFrame { get; }
	private static Money? LiquidityClue { get; set; }
	private static object LiquidityClueLock { get; } = new object();

	public static Money? GetLiquidityClue()
	{
		lock (LiquidityClueLock)
		{
			return LiquidityClue;
		}
	}

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
		RoundState? currentRoundState;
		uint256 excludeRound = uint256.Zero;
		ImmutableList<SmartCoin> coins;

		do
		{
			currentRoundState = await WaitForRoundAsync(excludeRound, cancellationToken).ConfigureAwait(false);
			RoundParameters roundParameteers = currentRoundState.CoinjoinState.Parameters;

			Money liquidityClue = roundParameteers.MaxSuggestedAmount;
			lock (LiquidityClueLock)
			{
				if (LiquidityClue is not null)
				{
					liquidityClue = Math.Min(LiquidityClue, liquidityClue);
				}
			}

			coins = SelectCoinsForRound(coinCandidates, roundParameteers, ConsolidationMode, AnonScoreTarget, RedCoinIsolation, liquidityClue, SecureRandom);

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
			throw new NoCoinsToMixException($"No coin was selected from '{coinCandidates.Count()}' number of coins. Probably it was not economical, total amount of coins were: {Money.Satoshis(coinCandidates.Sum(c => c.Amount))} BTC.");
		}

		// Keep going to blame round until there's none, so CJs won't be DDoS-ed.
		while (true)
		{
			CoinJoinResult result = await StartRoundAsync(coins, currentRoundState, cancellationToken).ConfigureAwait(false);
			if (!result.GoForBlameRound)
			{
				return result;
			}

			// Only use successfully registered coins in the blame round.
			coins = result.RegisteredCoins;

			currentRoundState.LogInfo("Waiting for the blame round.");
			currentRoundState = await WaitForBlameRoundAsync(currentRoundState.Id, cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("Blame rounds were not successful.");
	}

	public async Task<CoinJoinResult> StartRoundAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		var roundId = roundState.Id;

		ImmutableArray<(AliceClient AliceClient, PersonCircuit PersonCircuit)> registeredAliceClientAndCircuits = ImmutableArray<(AliceClient, PersonCircuit)>.Empty;

		// Because of the nature of the protocol, the input registration and the connection confirmation phases are done subsequently thus they have a merged timeout.
		var timeUntilOutputReg = (roundState.InputRegistrationEnd - DateTimeOffset.UtcNow) + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;

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
				roundState = ex.RoundState;
				var message = ex.RoundState.EndRoundState switch
				{
					EndRoundState.AbortedNotEnoughAlices => $"Not enough participants in the round to continue. Waiting for a new round.",
					_ => $"Registration phase ended by the coordinator: '{ex.Message}' code: '{ex.RoundState.EndRoundState}'."
				};

				roundState.LogInfo(message);
				return new CoinJoinResult(false);
			}

			if (!registeredAliceClientAndCircuits.Any())
			{
				return new CoinJoinResult(false);
			}

			roundState.LogInfo($"Successfully registered {registeredAliceClientAndCircuits.Length} inputs.");

			var registeredAliceClients = registeredAliceClientAndCircuits.Select(x => x.AliceClient).ToImmutableArray();

			var outputTxOuts = await ProceedWithOutputRegistrationPhaseAsync(roundId, registeredAliceClients, cancellationToken).ConfigureAwait(false);

			var (unsignedCoinJoin, aliceClientsThatSigned) = await ProceedWithSigningStateAsync(roundId, registeredAliceClients, outputTxOuts, cancellationToken).ConfigureAwait(false);

			roundState = await RoundStatusUpdater.CreateRoundAwaiter(s => s.Id == roundId && s.Phase == Phase.Ended, cancellationToken).ConfigureAwait(false);

			var msg = roundState.EndRoundState switch
			{
				EndRoundState.TransactionBroadcasted => $"Broadcasted. Coinjoin TxId: ({unsignedCoinJoin.GetHash()})",
				EndRoundState.TransactionBroadcastFailed => $"Failed to broadcast. Coinjoin TxId: ({unsignedCoinJoin.GetHash()})",
				EndRoundState.AbortedWithError => "Round abnormally finished.",
				EndRoundState.AbortedNotEnoughAlices => "Aborted. Not enough participants.",
				EndRoundState.AbortedNotEnoughAlicesSigned => "Aborted. Not enough participants signed the coinjoin transaction.",
				EndRoundState.NotAllAlicesSign => "Aborted. Some Alices didn't sign. Go to blame round.",
				EndRoundState.AbortedNotAllAlicesConfirmed => "Aborted. Some Alices didn't confirm.",
				EndRoundState.AbortedLoadBalancing => "Aborted. Load balancing registrations.",
				EndRoundState.None => "Unknown.",
				_ => throw new ArgumentOutOfRangeException()
			};
			roundState.LogInfo(msg);

			LogCoinJoinSummary(registeredAliceClients, outputTxOuts, unsignedCoinJoin, roundState);

			lock (LiquidityClueLock)
			{
				Money? liquidityClue = TryCalculateLiquidityClue(unsignedCoinJoin, outputTxOuts);

				// Dismiss pleb round.
				// If it's close to the max suggested amount then we shouldn't set it as the round is likely a pleb round.
				if (liquidityClue is not null
					&& (roundState.CoinjoinState.Parameters.MaxSuggestedAmount / 2) > liquidityClue)
				{
					LiquidityClue = liquidityClue;
				}
			}

			return new CoinJoinResult(
				GoForBlameRound: roundState.EndRoundState == EndRoundState.NotAllAlicesSign,
				SuccessfulBroadcast: roundState.EndRoundState == EndRoundState.TransactionBroadcasted,
				RegisteredCoins: aliceClientsThatSigned.Select(a => a.SmartCoin).ToImmutableList(),
				RegisteredOutputs: outputTxOuts.Select(o => o.ScriptPubKey).ToImmutableList());
		}
		finally
		{
			foreach (var coins in smartCoins)
			{
				coins.CoinJoinInProgress = false;
			}

			foreach (var aliceClientAndCircuit in registeredAliceClientAndCircuits)
			{
				aliceClientAndCircuit.AliceClient.Finish();
				aliceClientAndCircuit.PersonCircuit.Dispose();
			}
			CoinJoinClientProgress.SafeInvoke(this, new LeavingCriticalPhase());
			CoinJoinClientProgress.SafeInvoke(this, new RoundEnded(roundState));
		}
	}

	public static Money? TryCalculateLiquidityClue(Transaction coinjoin, IEnumerable<TxOut>? ownTxOuts = null)
	{
		var denoms = coinjoin.Outputs
				.Where(x =>
					BlockchainAnalyzer.StdDenoms.Contains(x.Value.Satoshi) // We only care about denom outputs as those can be considered reasonably mixed.
					&& !ownTxOuts?.Any(y => y.ScriptPubKey == x.ScriptPubKey && y.Value == x.Value) is true) // We only care about outputs those aren't ours.
				.Select(x => x.Value)
				.OrderByDescending(x => x)
				.Distinct()
				.ToArray();
		var topDenoms = denoms.Take((int)Math.Ceiling(denoms.Length * 10 / 100d)); // Take top 10% of denominations.
		if (topDenoms.Any())
		{
			return Money.Coins(topDenoms.Average(x => x.ToDecimal(MoneyUnit.BTC)));
		}
		else
		{
			return null;
		}
	}

	private async Task<ImmutableArray<(AliceClient AliceClient, PersonCircuit PersonCircuit)>> CreateRegisterAndConfirmCoinsAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancel)
	{
		int eventInvokedAlready = 0;

		var remainingInputRegTime = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow;

		using CancellationTokenSource strictInputRegTimeoutCts = new(remainingInputRegTime);
		using CancellationTokenSource inputRegTimeoutCts = new(remainingInputRegTime + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource connConfTimeoutCts = new(remainingInputRegTime + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource registrationsCts = new();
		using CancellationTokenSource confirmationsCts = new();

		using CancellationTokenSource linkedUnregisterCts = CancellationTokenSource.CreateLinkedTokenSource(strictInputRegTimeoutCts.Token, registrationsCts.Token);
		using CancellationTokenSource linkedRegistrationsCts = CancellationTokenSource.CreateLinkedTokenSource(inputRegTimeoutCts.Token, registrationsCts.Token, cancel);
		using CancellationTokenSource linkedConfirmationsCts = CancellationTokenSource.CreateLinkedTokenSource(connConfTimeoutCts.Token, confirmationsCts.Token, cancel);
		using CancellationTokenSource timeoutAndGlobalCts = CancellationTokenSource.CreateLinkedTokenSource(inputRegTimeoutCts.Token, connConfTimeoutCts.Token, cancel);

		async Task<(AliceClient? AliceClient, PersonCircuit? PersonCircuit)> RegisterInputAsync(SmartCoin coin)
		{
			PersonCircuit? personCircuit = null;
			try
			{
				personCircuit = HttpClientFactory.NewHttpClientWithPersonCircuit(out Tor.Http.IHttpClient httpClient);
				Tor.Http.IHttpClient httpClientReadyAndSigning = HttpClientFactory.NewHttpClientWithCircuitPerRequest();

				// Alice client requests are inherently linkable to each other, so the circuit can be reused
				var arenaRequestHandler = new WabiSabiHttpApiClient(httpClient);
				var arenaRequestHandlerReadyAndSigning = new WabiSabiHttpApiClient(httpClientReadyAndSigning);

				var aliceArenaClient = new ArenaClient(
					roundState.CreateAmountCredentialClient(SecureRandom),
					roundState.CreateVsizeCredentialClient(SecureRandom),
					CoordinatorIdentifier,
					arenaRequestHandler,
					arenaRequestHandlerReadyAndSigning);

				var aliceClient = await AliceClient.CreateRegisterAndConfirmInputAsync(roundState, aliceArenaClient, coin, KeyChain, RoundStatusUpdater, linkedUnregisterCts.Token, linkedRegistrationsCts.Token, linkedConfirmationsCts.Token).ConfigureAwait(false);

				// Right after the first real-cred confirmation happened we entered into critical phase.
				if (Interlocked.Exchange(ref eventInvokedAlready, 1) == 0)
				{
					CoinJoinClientProgress.SafeInvoke(this, new EnteringCriticalPhase());
				}

				return (aliceClient, personCircuit);
			}
			catch (WabiSabiProtocolException wpe)
			{
				if (wpe.ErrorCode is WabiSabiProtocolErrorCode.RoundNotFound)
				{
					// if the round does not exist then it ended/aborted.
					registrationsCts.Cancel();
					confirmationsCts.Cancel();
				}
				else if (wpe.ErrorCode is WabiSabiProtocolErrorCode.WrongPhase)
				{
					if (wpe.ExceptionData is WrongPhaseExceptionData wrongPhaseExceptionData)
					{
						if (wrongPhaseExceptionData.CurrentPhase != Phase.InputRegistration)
						{
							// Cancel all remaining pending input registrations because they will arrive late too.
							registrationsCts.Cancel();

							if (wrongPhaseExceptionData.CurrentPhase != Phase.ConnectionConfirmation)
							{
								// Cancel all remaining pending connection confirmations because they will arrive late too.
								confirmationsCts.Cancel();
							}
						}
					}
					else
					{
						throw new InvalidOperationException(
							$"Unexpected condition. {nameof(WrongPhaseException)} doesn't contain a {nameof(WrongPhaseExceptionData)} data field.");
					}
				}

				personCircuit?.Dispose();
				return (null, null);
			}
			catch (OperationCanceledException ex)
			{
				if (cancel.IsCancellationRequested)
				{
					Logger.LogDebug("User requested cancellation of registration and confirmation.");
				}
				else if (registrationsCts.IsCancellationRequested)
				{
					Logger.LogDebug("Registration was cancelled.");
				}
				else if (connConfTimeoutCts.IsCancellationRequested)
				{
					Logger.LogDebug("Connection confirmation was cancelled.");
				}
				else
				{
					Logger.LogDebug(ex);
				}

				personCircuit?.Dispose();
				return (null, null);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
				personCircuit?.Dispose();
				return (null, null);
			}
		}

		// Gets the list of scheduled dates/time in the remaining available time frame when each alice has to be registered.
		var remainingTimeForRegistration = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow;

		roundState.LogDebug($"Inputs({smartCoins.Count()}) registration started - it will end in: {remainingTimeForRegistration:hh\\:mm\\:ss}.");

		// Decrease the available time, so the clients hurry up.
		var safetyBuffer = TimeSpan.FromMinutes(1);
		var scheduledDates = GetScheduledDates(smartCoins.Count(), roundState.InputRegistrationEnd - safetyBuffer);

		// Creates scheduled tasks (tasks that wait until the specified date/time and then perform the real registration)
		var aliceClients = smartCoins.Zip(
			scheduledDates,
			async (coin, date) =>
			{
				var delay = date - DateTimeOffset.UtcNow;
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, timeoutAndGlobalCts.Token).ConfigureAwait(false);
				}
				return await RegisterInputAsync(coin).ConfigureAwait(false);
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
				CoordinatorIdentifier,
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

				try
				{
					await aliceClient.ReadyToSignAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					// This cannot fail. Otherwise the whole conjoin process will be halted.
					Logger.LogDebug(e.ToString());
					Logger.LogInfo($"Failed to register signal ready to sign with message {e.Message}. Ignoring...");
				}
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
		var totalCoordinationFee = Money.Satoshis(registeredAliceClients.Where(a => !a.IsPayingZeroCoordinationFee).Sum(a => roundParameters.CoordinationFeeRate.GetFee(a.SmartCoin.Amount)));

		string[] summary = new string[]
		{
			"",
			$"\tInput total: {totalInputAmount.ToString(true, false)} Eff: {totalEffectiveInputAmount.ToString(true, false)} NetwFee: {inputNetworkFee.ToString(true, false)} CoordFee: {totalCoordinationFee.ToString(true)}",
			$"\tOutpu total: {totalOutputAmount.ToString(true, false)} Eff: {totalEffectiveOutputAmount.ToString(true, false)} NetwFee: {outputNetworkFee.ToString(true, false)}",
			$"\tTotal diff : {totalDifference.ToString(true, false)}",
			$"\tEffec diff : {effectiveDifference.ToString(true, false)}",
			$"\tTotal fee  : {totalNetworkFee.ToString(true, false)}"
		};

		roundState.LogDebug(string.Join(Environment.NewLine, summary));
	}

	/// <param name="consolidationMode">If true it attempts to select as many coins as it can.</param>
	/// <param name="anonScoreTarget">Tries to select few coins over this threshold.</param>
	/// <param name="redCoinIsolation">If true, coins under anonscore 2 will not be selected together.</param>
	/// <param name="liquidityClue">Weakly prefer not to select inputs over this.</param>
	internal static ImmutableList<SmartCoin> SelectCoinsForRound(
		IEnumerable<SmartCoin> coins,
		RoundParameters parameters,
		bool consolidationMode,
		int anonScoreTarget,
		bool redCoinIsolation,
		Money liquidityClue,
		WasabiRandom rnd)
	{
		// Sanity check.
		if (liquidityClue <= Money.Zero)
		{
			liquidityClue = Constants.MaximumNumberOfBitcoinsMoney;
		}

		var filteredCoins = coins
			.Where(x => parameters.AllowedInputAmounts.Contains(x.Amount))
			.Where(x => parameters.AllowedInputTypes.Any(t => x.ScriptPubKey.IsScriptType(t)))
			.Where(x => x.EffectiveValue(parameters.MiningFeeRate) > Money.Zero)
			.ToArray();

		var privateCoins = filteredCoins
			.Where(x => x.HdPubKey.AnonymitySet >= anonScoreTarget)
			.ToArray();
		var semiPrivateCoins = filteredCoins
			.Where(x => x.HdPubKey.AnonymitySet < anonScoreTarget && x.HdPubKey.AnonymitySet >= 2)
			.ToArray();
		var redCoins = filteredCoins
			.Where(x => x.HdPubKey.AnonymitySet < 2)
			.ToArray();

		if (semiPrivateCoins.Length + redCoins.Length == 0)
		{
			// Let's not mess up the logs when this function gets called many times.
			return ImmutableList<SmartCoin>.Empty;
		}

		Logger.LogDebug($"Coin selection started:");
		Logger.LogDebug($"{nameof(filteredCoins)}: {filteredCoins.Length} coins, valued at {Money.Satoshis(filteredCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug($"{nameof(privateCoins)}: {privateCoins.Length} coins, valued at {Money.Satoshis(privateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug($"{nameof(semiPrivateCoins)}: {semiPrivateCoins.Length} coins, valued at {Money.Satoshis(semiPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug($"{nameof(redCoins)}: {redCoins.Length} coins, valued at {Money.Satoshis(redCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		// If we want to isolate red coins from each other, then only let a single red coin get into our selection candidates.
		var allowedNonPrivateCoins = semiPrivateCoins.ToList();
		if (redCoinIsolation)
		{
			var red = redCoins.RandomElement();
			if (red is not null)
			{
				allowedNonPrivateCoins.Add(red);
				Logger.LogDebug($"One red coin got selected: {red.Amount.ToString(false, true)} BTC. Isolating the rest.");
			}
		}
		else
		{
			allowedNonPrivateCoins.AddRange(redCoins);
		}

		Logger.LogDebug($"{nameof(allowedNonPrivateCoins)}: {allowedNonPrivateCoins.Count} coins, valued at {Money.Satoshis(allowedNonPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		int inputCount = Math.Min(
			privateCoins.Length + allowedNonPrivateCoins.Count,
			consolidationMode ? MaxInputsRegistrableByWallet : GetInputTarget(rnd));
		if (consolidationMode)
		{
			Logger.LogDebug($"Consolidation mode is on.");
		}
		Logger.LogDebug($"Targeted {nameof(inputCount)}: {inputCount}.");

		var biasShuffledPrivateCoins = AnonScoreTxSourceBiasedShuffle(privateCoins).ToArray();

		// Deprioritize private coins those are too large.
		var smallerPrivateCoins = biasShuffledPrivateCoins.Where(x => x.Amount <= liquidityClue);
		var largerPrivateCoins = biasShuffledPrivateCoins.Where(x => x.Amount > liquidityClue);

		// Let's allow only inputCount - 1 private coins to play.
		var allowedPrivateCoins = smallerPrivateCoins.Concat(largerPrivateCoins).Take(inputCount - 1).ToArray();
		Logger.LogDebug($"{nameof(allowedPrivateCoins)}: {allowedPrivateCoins.Length} coins, valued at {Money.Satoshis(allowedPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		var allowedCoins = allowedNonPrivateCoins.Concat(allowedPrivateCoins).ToArray();
		Logger.LogDebug($"{nameof(allowedCoins)}: {allowedCoins.Length} coins, valued at {Money.Satoshis(allowedCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		// Shuffle coins, while randomly biasing towards lower AS.
		var orderedAllowedCoins = AnonScoreTxSourceBiasedShuffle(allowedCoins).ToArray();

		// Always use the largest amounts, so we do not participate with insignificant amounts and fragment wallet needlessly.
		var largestNonPrivateCoins = allowedNonPrivateCoins
			.OrderByDescending(x => x.Amount)
			.Take(3)
			.ToArray();
		Logger.LogDebug($"Largest non-private coins: {string.Join(", ", largestNonPrivateCoins.Select(x => x.Amount.ToString(false, true)).ToArray())} bitcoins.");

		// Select a group of coins those are close to each other by anonymity score.
		Dictionary<int, IEnumerable<SmartCoin>> groups = new();

		// Create a bunch of combinations.
		var sw1 = Stopwatch.StartNew();
		foreach (var coin in largestNonPrivateCoins)
		{
			// Create a base combination just in case.
			var baseGroup = orderedAllowedCoins.Except(new[] { coin }).Take(inputCount - 1).Concat(new[] { coin });
			TryAddGroup(parameters, groups, baseGroup);

			var sw2 = Stopwatch.StartNew();
			foreach (var group in orderedAllowedCoins
				.Except(new[] { coin })
				.CombinationsWithoutRepetition(inputCount - 1)
				.Select(x => x.Concat(new[] { coin })))
			{
				TryAddGroup(parameters, groups, group);

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

		if (!groups.Any())
		{
			Logger.LogDebug($"Couldn't create any combinations, ending.");
			return ImmutableList<SmartCoin>.Empty;
		}
		Logger.LogDebug($"Created {groups.Count} combinations within {(int)sw1.Elapsed.TotalSeconds} seconds.");

		// Select the group where the less coins coming from the same tx.
		var bestRep = groups.Values.Select(x => GetReps(x)).Min(x => x);
		var bestRepGroups = groups.Values.Where(x => GetReps(x) == bestRep);
		Logger.LogDebug($"{nameof(bestRep)}: {bestRep}.");
		Logger.LogDebug($"Filtered combinations down to {nameof(bestRepGroups)}: {bestRepGroups.Count()}.");

		var remainingLargestNonPrivateCoins = largestNonPrivateCoins.Where(x => bestRepGroups.Any(y => y.Contains(x)));
		Logger.LogDebug($"Remaining largest non-private coins: {string.Join(", ", remainingLargestNonPrivateCoins.Select(x => x.Amount.ToString(false, true)).ToArray())} bitcoins.");

		// Bias selection towards larger numbers.
		var selectedNonPrivateCoin = remainingLargestNonPrivateCoins.RandomElement(); // Select randomly at first just to have a starting value.
		foreach (var coin in remainingLargestNonPrivateCoins.OrderByDescending(x => x.Amount))
		{
			if (rnd.GetInt(1, 101) <= 50)
			{
				selectedNonPrivateCoin = coin;
				break;
			}
		}
		if (selectedNonPrivateCoin is null)
		{
			Logger.LogDebug($"Couldn't select largest non-private coin, ending.");
			return ImmutableList<SmartCoin>.Empty;
		}
		Logger.LogDebug($"Randomly selected large non-private coin: {selectedNonPrivateCoin.Amount.ToString(false, true)}.");

		var finalCandidate = bestRepGroups
			.Where(x => x.Contains(selectedNonPrivateCoin))
			.RandomElement();
		if (finalCandidate is null)
		{
			Logger.LogDebug($"Couldn't select final selection candidate, ending.");
			return ImmutableList<SmartCoin>.Empty;
		}
		Logger.LogDebug($"Selected the final selection candidate: {finalCandidate.Count()} coins, {string.Join(", ", finalCandidate.Select(x => x.Amount.ToString(false, true)).ToArray())} bitcoins.");

		// Let's remove some coins coming from the same tx in the final candidate:
		// The smaller our balance is the more privacy we gain and the more the user cares about the costs, so more interconnectedness allowance makes sense.
		var toRegister = finalCandidate.Sum(x => x.Amount);
		int percent;
		if (toRegister < 10_000)
		{
			percent = 20;
		}
		else if (toRegister < 100_000)
		{
			percent = 30;
		}
		else if (toRegister < 1_000_000)
		{
			percent = 40;
		}
		else if (toRegister < 10_000_000)
		{
			percent = 50;
		}
		else if (toRegister < 100_000_000) // 1 BTC
		{
			percent = 60;
		}
		else if (toRegister < 1_000_000_000)
		{
			percent = 70;
		}
		else
		{
			percent = 80;
		}

		int sameTxAllowance = GetRandomBiasedSameTxAllowance(rnd, percent);

		var winner = new List<SmartCoin>();
		winner.Add(selectedNonPrivateCoin);
		foreach (var coin in finalCandidate
			.Except(new[] { selectedNonPrivateCoin })
			.OrderBy(x => x.HdPubKey.AnonymitySet)
			.ThenByDescending(x => x.Amount))
		{
			// If the coin is coming from same tx, then check our allowance.
			if (winner.Any(x => x.TransactionId == coin.TransactionId))
			{
				var sameTxUsed = winner.Count - winner.Select(x => x.TransactionId).Distinct().Count();
				if (sameTxUsed < sameTxAllowance)
				{
					winner.Add(coin);
				}
			}
			else
			{
				winner.Add(coin);
			}
		}

		double winnerAnonLoss = GetAnonLoss(winner);
		while ((winner.Sum(x => x.Amount) > liquidityClue) && (winnerAnonLoss > MaxWeightedAnonLoss))
		{
			List<SmartCoin> bestReducedWinner = winner;
			var bestAnonLoss = winnerAnonLoss;

			foreach (SmartCoin coin in winner.Except(new[] { selectedNonPrivateCoin }))
			{
				var reducedWinner = winner.Except(new[] { coin });
				var anonLoss = GetAnonLoss(reducedWinner);

				if (anonLoss <= bestAnonLoss)
				{
					bestAnonLoss = anonLoss;
					bestReducedWinner = reducedWinner.ToList();
				}
			}

			winner = bestReducedWinner;
			winnerAnonLoss = bestAnonLoss;
		}

		if (winner.Count != finalCandidate.Count())
		{
			Logger.LogDebug($"Optimizing selection, removing coins coming from the same tx.");
			Logger.LogDebug($"{nameof(sameTxAllowance)}: {sameTxAllowance}.");
			Logger.LogDebug($"{nameof(winner)}: {winner.Count} coins, {string.Join(", ", winner.Select(x => x.Amount.ToString(false, true)).ToArray())} bitcoins.");
		}

		return winner.ToShuffled()?.ToImmutableList() ?? ImmutableList<SmartCoin>.Empty;
	}

	private static double GetAnonLoss(IEnumerable<SmartCoin> coins)
	{
		double minimumAnonScore = coins.Min(x => x.HdPubKey.AnonymitySet);
		return coins.Sum(x => (x.HdPubKey.AnonymitySet - minimumAnonScore) * x.Amount.Satoshi) / coins.Sum(x => x.Amount.Satoshi);
	}

	private static int GetRandomBiasedSameTxAllowance(WasabiRandom rnd, int percent)
	{
		for (int num = 0; num <= 100; num++)
		{
			if (rnd.GetInt(1, 101) <= percent)
			{
				return num;
			}
		}

		return 0;
	}

	private static IEnumerable<SmartCoin> AnonScoreTxSourceBiasedShuffle(SmartCoin[] coins)
	{
		var orderedCoins = new List<SmartCoin>();
		for (int i = 0; i < coins.Length; i++)
		{
			// Order by anonscore first.
			var remaining = coins.Except(orderedCoins).OrderBy(x => x.HdPubKey.AnonymitySet);

			// Then manipulate the list so repeating tx sources go to the end.
			var alternating = new List<SmartCoin>();
			var skipped = new List<SmartCoin>();
			foreach (var c in remaining)
			{
				if (alternating.Any(x => x.TransactionId == c.TransactionId) || orderedCoins.Any(x => x.TransactionId == c.TransactionId))
				{
					skipped.Add(c);
				}
				else
				{
					alternating.Add(c);
				}
			}
			alternating.AddRange(skipped);

			var coin = alternating.BiasedRandomElement(50);
			if (coin is null)
			{
				throw new NotSupportedException("This is impossible.");
			}

			orderedCoins.Add(coin);
			yield return coin;
		}
	}

	private static bool TryAddGroup(RoundParameters parameters, Dictionary<int, IEnumerable<SmartCoin>> groups, IEnumerable<SmartCoin> group)
	{
		var inSum = group.Sum(x => x.EffectiveValue(parameters.MiningFeeRate, parameters.CoordinationFeeRate));
		var outFee = parameters.MiningFeeRate.GetFee(Constants.P2wpkhOutputVirtualSize);
		if (inSum >= outFee + parameters.AllowedOutputAmounts.Min)
		{
			var k = HashCode.Combine(group.OrderBy(x => x.TransactionId).ThenBy(x => x.Index));
			return groups.TryAdd(k, group);
		}

		return false;
	}

	private static int GetReps(IEnumerable<SmartCoin> group)
		=> group.GroupBy(x => x.TransactionId).Sum(coinsInTxGroup => coinsInTxGroup.Count() - 1);

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
	/// Calculates how many inputs are desirable to be registered.
	/// Note: random biasing is applied.
	/// </summary>
	/// <returns>Desired input count.</returns>
	private static int GetInputTarget(WasabiRandom rnd)
	{
		// Until our UTXO count target isn't reached, let's register as few coins as we can to reach it.
		int targetInputCount = MaxInputsRegistrableByWallet;

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

		// Splitting the remaining time.
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

		var combinedToken = linkedCts.Token;
		try
		{
			// Re-issuances.
			var bobClient = CreateBobClient(roundState);
			roundState.LogInfo("Starting reissuances.");
			await scheduler.StartReissuancesAsync(registeredAliceClients, bobClient, combinedToken).ConfigureAwait(false);

			// Output registration.
			roundState.LogDebug($"Output registration started - it will end in: {outputRegistrationEndTime - DateTimeOffset.UtcNow:hh\\:mm\\:ss}.");

			var outputRegistrationScheduledDates = GetScheduledDates(outputTxOuts.Count(), outputRegistrationEndTime, MaximumRequestDelay);
			await scheduler.StartOutputRegistrationsAsync(outputTxOuts, bobClient, KeyChain, outputRegistrationScheduledDates, combinedToken).ConfigureAwait(false);
			roundState.LogInfo($"Outputs({outputTxOuts.Count()}) were registered.");
		}
		catch (Exception e)
		{
			roundState.LogInfo($"Failed to register outputs with message {e.Message}. Ignoring...");
			roundState.LogDebug(e.ToString());
		}

		// ReadyToSign.
		roundState.LogDebug($"ReadyToSign phase started - it will end in: {readyToSignEndTime - DateTimeOffset.UtcNow:hh\\:mm\\:ss}.");
		await ReadyToSignAsync(registeredAliceClients, readyToSignEndTime, combinedToken).ConfigureAwait(false);
		roundState.LogDebug($"Alices({registeredAliceClients.Length}) are ready to sign.");
		return outputTxOuts;
	}

	private async Task<(Transaction UnsignedCoinJoin, ImmutableArray<AliceClient> AliceClientsThatSigned)>
		ProceedWithSigningStateAsync(uint256 roundId, ImmutableArray<AliceClient> registeredAliceClients, IEnumerable<TxOut> outputTxOuts, CancellationToken cancellationToken)
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

		// If everything is okay, then sign all the inputs. Otherwise, in case there are missing outputs, the server is
		// lying (it lied us before when it responded with 200 OK to the OutputRegistration requests or it is lying us
		// now when we identify as satoshi.
		// In this scenario we should ban the coordinator and stop dealing with it.
		// see more: https://github.com/zkSNACKs/WalletWasabi/issues/8171
		bool mustSignAllInputs = SanityCheck(outputTxOuts, unsignedCoinJoin);
		if (!mustSignAllInputs)
		{
			roundState.LogInfo($"There are missing outputs. A subset of inputs will be signed.");
		}

		// Send signature.
		var combinedToken = linkedCts.Token;
		var alicesToSign = mustSignAllInputs
			? registeredAliceClients
			: registeredAliceClients.RemoveAt(SecureRandom.GetInt(0, registeredAliceClients.Length));

		await SignTransactionAsync(alicesToSign, unsignedCoinJoin, signingStateEndTime, combinedToken).ConfigureAwait(false);
		roundState.LogInfo($"{alicesToSign.Length} out of {registeredAliceClients.Length} Alices have signed the coinjoin tx.");

		return (unsignedCoinJoin, alicesToSign);
	}

	private async Task<ImmutableArray<(AliceClient, PersonCircuit)>> ProceedWithInputRegAndConfirmAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		CoinJoinClientProgress.SafeInvoke(this, new EnteringInputRegistrationPhase(roundState, roundState.InputRegistrationEnd));

		// Register coins.
		var result = await CreateRegisterAndConfirmCoinsAsync(smartCoins, roundState, cancellationToken).ConfigureAwait(false);

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
