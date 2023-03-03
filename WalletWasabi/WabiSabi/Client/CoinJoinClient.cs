using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
	private const int MaxWeightedAnonLoss = 3; // Maximum tolerable WeightedAnonLoss.

	private static readonly Money MinimumOutputAmountSanity = Money.Coins(0.0001m); // ignore rounds with too big minimum denominations
	private static readonly TimeSpan ExtraPhaseTimeoutMargin = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan ExtraRoundTimeoutMargin = TimeSpan.FromMinutes(10);

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
		LiquidityClueProvider liquidityClueProvider,
		int anonScoreTarget = int.MaxValue,
		bool consolidationMode = false,
		bool redCoinIsolation = false,
		TimeSpan feeRateMedianTimeFrame = default,
		TimeSpan doNotRegisterInLastMinuteTimeLimit = default)
	{
		HttpClientFactory = httpClientFactory;
		KeyChain = keyChain;
		DestinationProvider = destinationProvider;
		RoundStatusUpdater = roundStatusUpdater;
		AnonScoreTarget = anonScoreTarget;
		CoordinatorIdentifier = coordinatorIdentifier;
		LiquidityClueProvider = liquidityClueProvider;
		ConsolidationMode = consolidationMode;
		SemiPrivateThreshold = redCoinIsolation ? Constants.SemiPrivateThreshold : 0;
		FeeRateMedianTimeFrame = feeRateMedianTimeFrame;
		SecureRandom = new SecureRandom();
		DoNotRegisterInLastMinuteTimeLimit = doNotRegisterInLastMinuteTimeLimit;
	}

	public event EventHandler<CoinJoinProgressEventArgs>? CoinJoinClientProgress;

	private SecureRandom SecureRandom { get; }
	private IWasabiHttpClientFactory HttpClientFactory { get; }
	private IKeyChain KeyChain { get; }
	private IDestinationProvider DestinationProvider { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	private string CoordinatorIdentifier { get; }
	private LiquidityClueProvider LiquidityClueProvider { get; }
	private int AnonScoreTarget { get; }
	private TimeSpan DoNotRegisterInLastMinuteTimeLimit { get; }

	private bool ConsolidationMode { get; set; }
	private bool RedCoinIsolation { get; }
	private int SemiPrivateThreshold { get; }
	private TimeSpan FeeRateMedianTimeFrame { get; }

	private async Task<RoundState> WaitForRoundAsync(uint256 excludeRound, CancellationToken token)
	{
		CoinJoinClientProgress.SafeInvoke(this, new WaitingForRound());
		return await RoundStatusUpdater
			.CreateRoundAwaiterAsync(
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
				.CreateRoundAwaiterAsync(
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

			var liquidityClue = LiquidityClueProvider.GetLiquidityClue(roundParameteers.MaxSuggestedAmount);
			var utxoSelectionParameters = UtxoSelectionParameters.FromRoundParameters(roundParameteers);
			coins = SelectCoinsForRound(coinCandidates, utxoSelectionParameters, ConsolidationMode, AnonScoreTarget, SemiPrivateThreshold, liquidityClue, SecureRandom);

			if (!roundParameteers.AllowedInputTypes.Contains(ScriptType.P2WPKH) || !roundParameteers.AllowedOutputTypes.Contains(ScriptType.P2WPKH))
			{
				excludeRound = currentRoundState.Id;
				currentRoundState.LogInfo($"Skipping the round since it doesn't support P2WPKH inputs and outputs.");

				continue;
			}

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
			using CancellationTokenSource coinJoinRoundTimeoutCts = new(
				currentRoundState.InputRegistrationTimeout +
				currentRoundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout +
				currentRoundState.CoinjoinState.Parameters.OutputRegistrationTimeout +
				currentRoundState.CoinjoinState.Parameters.TransactionSigningTimeout +
				ExtraRoundTimeoutMargin);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, coinJoinRoundTimeoutCts.Token);

			var result = await StartRoundAsync(coins, currentRoundState, linkedCts.Token).ConfigureAwait(false);

			switch (result)
			{
				case DisruptedCoinJoinResult info:
					// Only use successfully registered coins in the blame round.
					coins = info.SignedCoins;

					currentRoundState.LogInfo("Waiting for the blame round.");
					currentRoundState = await WaitForBlameRoundAsync(currentRoundState.Id, cancellationToken).ConfigureAwait(false);
					break;

				case SuccessfulCoinJoinResult success:
					return success;

				case FailedCoinJoinResult failure:
					return failure;

				default:
					throw new InvalidOperationException("The coinjoin result type was not handled.");
			}
		}

		throw new InvalidOperationException("Blame rounds were not successful.");
	}

	public async Task<CoinJoinResult> StartRoundAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		var roundId = roundState.Id;

		// the task is watching if the round ends during operations. If it does it will trigger cancellation.
		using CancellationTokenSource waitRoundEndedTaskCts = new();
		using CancellationTokenSource roundEndedCts = new();
		var waitRoundEndedTask = Task.Run(async () =>
		{
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(waitRoundEndedTaskCts.Token, cancellationToken);
			var rs = await RoundStatusUpdater.CreateRoundAwaiterAsync(s => s.Id == roundId && s.Phase == Phase.Ended, linkedCts.Token).ConfigureAwait(false);

			// Indicate that the round was ended. Cancel ongoing operations those are using this CTS.
			roundEndedCts.Cancel();
			return rs;
		});

		try
		{
			ImmutableArray<AliceClient> aliceClientsThatSigned = ImmutableArray<AliceClient>.Empty;
			IEnumerable<TxOut> outputTxOuts = Enumerable.Empty<TxOut>();
			Transaction? unsignedCoinJoin = null;
			try
			{
				using CancellationTokenSource cancelOrRoundEndedCts = CancellationTokenSource.CreateLinkedTokenSource(roundEndedCts.Token, cancellationToken);
				(aliceClientsThatSigned, outputTxOuts, unsignedCoinJoin) = await ProceedWithRoundAsync(roundState, smartCoins, cancelOrRoundEndedCts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// If the Round ended we let the execution continue, otherwise throw.
				if (!roundEndedCts.IsCancellationRequested)
				{
					throw;
				}
			}
			catch (UnexpectedRoundPhaseException ex) when (ex.Actual == Phase.Ended)
			{
				// Do nothing - if the actual state of the round is Ended we let the execution continue.
			}

			roundState = await waitRoundEndedTask.ConfigureAwait(false);

			var hash = unsignedCoinJoin is { } tx ? tx.GetHash().ToString() : "Not available";

			var msg = roundState.EndRoundState switch
			{
				EndRoundState.TransactionBroadcasted => $"Broadcasted. Coinjoin TxId: ({hash})",
				EndRoundState.TransactionBroadcastFailed => $"Failed to broadcast. Coinjoin TxId: ({hash})",
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
			var signedCoins = aliceClientsThatSigned.Select(a => a.SmartCoin).ToImmutableList();

			return roundState.EndRoundState switch
			{
				EndRoundState.TransactionBroadcasted => new SuccessfulCoinJoinResult(
					Coins: signedCoins,
					OutputScripts: outputTxOuts.Select(o => o.ScriptPubKey).ToImmutableList(),
					UnsignedCoinJoin: unsignedCoinJoin!),
				EndRoundState.NotAllAlicesSign => new DisruptedCoinJoinResult(signedCoins),
				_ => new FailedCoinJoinResult()
			};
		}
		finally
		{
			// Cancel and handle the task.
			waitRoundEndedTaskCts.Cancel();
			try
			{
				_ = await waitRoundEndedTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// Make sure that to not generate UnobserverTaskException.
				roundState.LogDebug(ex.Message);
			}

			CoinJoinClientProgress.SafeInvoke(this, new LeavingCriticalPhase());

			// Try to update to the latest roundState.
			var currentRoundState = RoundStatusUpdater.TryGetRoundState(roundState.Id, out var latestRoundState) ? latestRoundState : roundState;
			CoinJoinClientProgress.SafeInvoke(this, new RoundEnded(currentRoundState));
		}
	}

	private async Task<(ImmutableArray<AliceClient> aliceClientsThatSigned, IEnumerable<TxOut> OutputTxOuts, Transaction UnsignedCoinJoin)> ProceedWithRoundAsync(RoundState roundState, IEnumerable<SmartCoin> smartCoins, CancellationToken cancellationToken)
	{
		ImmutableArray<(AliceClient AliceClient, PersonCircuit PersonCircuit)> registeredAliceClientAndCircuits = ImmutableArray<(AliceClient, PersonCircuit)>.Empty;
		try
		{
			var roundId = roundState.Id;

			registeredAliceClientAndCircuits = await ProceedWithInputRegAndConfirmAsync(smartCoins, roundState, cancellationToken).ConfigureAwait(false);
			if (!registeredAliceClientAndCircuits.Any())
			{
				throw new NoCoinsToMixException($"The coordinator rejected all {smartCoins.Count()} inputs.");
			}

			roundState.LogInfo($"Successfully registered {registeredAliceClientAndCircuits.Length} inputs.");

			var registeredAliceClients = registeredAliceClientAndCircuits.Select(x => x.AliceClient).ToImmutableArray();

			var outputTxOuts = await ProceedWithOutputRegistrationPhaseAsync(roundId, registeredAliceClients, cancellationToken).ConfigureAwait(false);

			var (unsignedCoinJoin, aliceClientsThatSigned) = await ProceedWithSigningStateAsync(roundId, registeredAliceClients, outputTxOuts, cancellationToken).ConfigureAwait(false);
			LogCoinJoinSummary(registeredAliceClients, outputTxOuts, unsignedCoinJoin, roundState);

			LiquidityClueProvider.UpdateLiquidityClue(roundState.CoinjoinState.Parameters.MaxSuggestedAmount, unsignedCoinJoin, outputTxOuts);

			return (aliceClientsThatSigned, outputTxOuts, unsignedCoinJoin);
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
		}
	}

	private async Task<ImmutableArray<(AliceClient AliceClient, PersonCircuit PersonCircuit)>> CreateRegisterAndConfirmCoinsAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancel)
	{
		int eventInvokedAlready = 0;

		UnexpectedRoundPhaseException? lastUnexpectedRoundPhaseException = null;

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
			bool disposeCircuit = true;

			try
			{
				personCircuit = HttpClientFactory.NewHttpClientWithPersonCircuit(out Tor.Http.IHttpClient httpClient);

				// Alice client requests are inherently linkable to each other, so the circuit can be reused
				var arenaRequestHandler = new WabiSabiHttpApiClient(httpClient);

				var aliceArenaClient = new ArenaClient(
					roundState.CreateAmountCredentialClient(SecureRandom),
					roundState.CreateVsizeCredentialClient(SecureRandom),
					CoordinatorIdentifier,
					arenaRequestHandler);

				var aliceClient = await AliceClient.CreateRegisterAndConfirmInputAsync(roundState, aliceArenaClient, coin, KeyChain, RoundStatusUpdater, linkedUnregisterCts.Token, linkedRegistrationsCts.Token, linkedConfirmationsCts.Token).ConfigureAwait(false);

				// Right after the first real-cred confirmation happened we entered into critical phase.
				if (Interlocked.Exchange(ref eventInvokedAlready, 1) == 0)
				{
					CoinJoinClientProgress.SafeInvoke(this, new EnteringCriticalPhase());
				}

				// Do not dispose the circuit, it will be used later.
				disposeCircuit = false;
				return (aliceClient, personCircuit);
			}
			catch (WabiSabiProtocolException wpe)
			{
				if (wpe.ErrorCode is WabiSabiProtocolErrorCode.RoundNotFound)
				{
					// if the round does not exist then it ended/aborted.
					registrationsCts.Cancel();
					confirmationsCts.Cancel();
					roundState.LogInfo($"Aborting input registrations: '{WabiSabiProtocolErrorCode.RoundNotFound}'.");
				}
				else if (wpe.ErrorCode is WabiSabiProtocolErrorCode.WrongPhase)
				{
					if (wpe.ExceptionData is WrongPhaseExceptionData wrongPhaseExceptionData)
					{
						roundState.LogInfo($"Aborting input registrations: '{WabiSabiProtocolErrorCode.WrongPhase}'.");
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
			}
			catch (UnexpectedRoundPhaseException ex)
			{
				lastUnexpectedRoundPhaseException = ex;
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				if (disposeCircuit)
				{
					personCircuit?.Dispose();
				}
			}

			// In case of any exception.
			return (null, null);
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

		var successfulAlices = aliceClients
			.Select(x => x.Result)
			.Where(r => r.AliceClient is not null && r.PersonCircuit is not null)
			.Select(r => (r.AliceClient!, r.PersonCircuit!))
			.ToImmutableArray();

		if (!successfulAlices.Any() && lastUnexpectedRoundPhaseException is { })
		{
			// In this case the coordinator aborted the round - throw only one exception and log outside.
			throw lastUnexpectedRoundPhaseException;
		}

		return successfulAlices;
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

	internal static bool SanityCheck(IEnumerable<TxOut> expectedOutputs, IEnumerable<TxOut> coinJoinOutputs)
	{
		bool AllExpectedScriptsArePresent() =>
			coinJoinOutputs
				.Select(x => x.ScriptPubKey)
				.IsSuperSetOf(expectedOutputs.Select(x => x.ScriptPubKey));

		bool AllOutputsHaveAtLeastTheExpectedValue() =>
			coinJoinOutputs
				.Join(
					expectedOutputs,
					x => x.ScriptPubKey,
					x => x.ScriptPubKey,
					(coinjoinOutput, expectedOutput) => coinjoinOutput.Value - expectedOutput.Value)
				.All(x => x >= 0L);

		return AllExpectedScriptsArePresent() && AllOutputsHaveAtLeastTheExpectedValue();
	}

	private async Task SignTransactionAsync(
		IEnumerable<AliceClient> aliceClients,
		TransactionWithPrecomputedData unsignedCoinJoinTransaction,
		DateTimeOffset signingEndTime,
		CancellationToken cancellationToken)
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
				try
				{
					using (BenchmarkLogger.Measure(LogLevel.Debug, nameof(SignTransactionAsync)))
					{
						await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, KeyChain, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (WabiSabiProtocolException ex) when (ex.ErrorCode == WabiSabiProtocolErrorCode.WitnessAlreadyProvided)
				{
					Logger.LogDebug("Signature was already sent - bypassing error.", ex);
				}
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
		var totalCoordinationFee = Money.Satoshis(registeredAliceClients.Where(a => !a.IsCoordinationFeeExempted).Sum(a => roundParameters.CoordinationFeeRate.GetFee(a.SmartCoin.Amount)));

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
	/// <param name="semiPrivateThreshold">Minimum anonymity of coins that can be selected together.</param>
	/// <param name="liquidityClue">Weakly prefer not to select inputs over this.</param>
	public static ImmutableList<TCoin> SelectCoinsForRound<TCoin>(
		IEnumerable<TCoin> coins,
		UtxoSelectionParameters parameters,
		bool consolidationMode,
		int anonScoreTarget,
		int semiPrivateThreshold,
		Money liquidityClue,
		WasabiRandom rnd)
		where TCoin : class, ISmartCoin, IEquatable<TCoin>
	{
		if (semiPrivateThreshold < 0)
		{
			throw new ArgumentException("Cannot be negative", nameof(semiPrivateThreshold));
		}

		// Sanity check.
		if (liquidityClue <= Money.Zero)
		{
			liquidityClue = Constants.MaximumNumberOfBitcoinsMoney;
		}

		var filteredCoins = coins
			.Where(x => parameters.AllowedInputAmounts.Contains(x.Amount))
			.Where(x => parameters.AllowedInputScriptTypes.Contains(x.ScriptType))
			.Where(x => x.EffectiveValue(parameters.MiningFeeRate) > Money.Zero)
			.ToArray();

		var privateCoins = filteredCoins
			.Where(x => x.IsPrivate(anonScoreTarget))
			.ToArray();
		var semiPrivateCoins = filteredCoins
			.Where(x => x.IsSemiPrivate(anonScoreTarget, semiPrivateThreshold))
			.ToArray();

		// redCoins will only fill up if redCoinIsolaton is turned on. Otherwise the coin will be in semiPrivateCoins.
		var redCoins = filteredCoins
			.Where(x => x.IsRedCoin(semiPrivateThreshold))
			.ToArray();

		if (semiPrivateCoins.Length + redCoins.Length == 0)
		{
			// Let's not mess up the logs when this function gets called many times.
			return ImmutableList<TCoin>.Empty;
		}

		Logger.LogDebug($"Coin selection started:");
		Logger.LogDebug($"{nameof(filteredCoins)}: {filteredCoins.Length} coins, valued at {Money.Satoshis(filteredCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug($"{nameof(privateCoins)}: {privateCoins.Length} coins, valued at {Money.Satoshis(privateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug($"{nameof(semiPrivateCoins)}: {semiPrivateCoins.Length} coins, valued at {Money.Satoshis(semiPrivateCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");
		Logger.LogDebug($"{nameof(redCoins)}: {redCoins.Length} coins, valued at {Money.Satoshis(redCoins.Sum(x => x.Amount)).ToString(false, true)} BTC.");

		// We want to isolate red coins from each other. We only let a single red coin get into our selection candidates.
		var allowedNonPrivateCoins = semiPrivateCoins.ToList();
		var red = redCoins.RandomElement();
		if (red is not null)
		{
			allowedNonPrivateCoins.Add(red);
			Logger.LogDebug($"One red coin got selected: {red.Amount.ToString(false, true)} BTC. Isolating the rest.");
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
		Logger.LogDebug($"Largest non-private coins: {string.Join(", ", largestNonPrivateCoins.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");

		// Select a group of coins those are close to each other by anonymity score.
		Dictionary<int, IEnumerable<TCoin>> groups = new();

		// Create a bunch of combinations.
		var sw1 = Stopwatch.StartNew();
		foreach (var coin in largestNonPrivateCoins)
		{
			// Create a base combination just in case.
			var baseGroup = orderedAllowedCoins.Except(new[] { coin }).Take(inputCount - 1).Concat(new[] { coin });
			TryAddGroup(parameters, groups, baseGroup);

			var sw2 = Stopwatch.StartNew();
			foreach (var group in orderedAllowedCoins
				.Except(new TCoin[] { coin })
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
			return ImmutableList<TCoin>.Empty;
		}
		Logger.LogDebug($"Created {groups.Count} combinations within {(int)sw1.Elapsed.TotalSeconds} seconds.");

		// Select the group where the less coins coming from the same tx.
		var bestRep = groups.Values.Select(x => GetReps(x)).Min(x => x);
		var bestRepGroups = groups.Values.Where(x => GetReps(x) == bestRep);
		Logger.LogDebug($"{nameof(bestRep)}: {bestRep}.");
		Logger.LogDebug($"Filtered combinations down to {nameof(bestRepGroups)}: {bestRepGroups.Count()}.");

		var remainingLargestNonPrivateCoins = largestNonPrivateCoins.Where(x => bestRepGroups.Any(y => y.Contains(x)));
		Logger.LogDebug($"Remaining largest non-private coins: {string.Join(", ", remainingLargestNonPrivateCoins.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");

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
			return ImmutableList<TCoin>.Empty;
		}
		Logger.LogDebug($"Randomly selected large non-private coin: {selectedNonPrivateCoin.Amount.ToString(false, true)}.");

		var finalCandidate = bestRepGroups
			.Where(x => x.Contains(selectedNonPrivateCoin))
			.RandomElement();
		if (finalCandidate is null)
		{
			Logger.LogDebug($"Couldn't select final selection candidate, ending.");
			return ImmutableList<TCoin>.Empty;
		}
		Logger.LogDebug($"Selected the final selection candidate: {finalCandidate.Count()} coins, {string.Join(", ", finalCandidate.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");

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

		List<TCoin> winner = new()
		{
			selectedNonPrivateCoin
		};

		foreach (var coin in finalCandidate
			.Except(new[] { selectedNonPrivateCoin })
			.OrderBy(x => x.AnonymitySet)
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

		// Only stay in the while if we are above the liquidityClue (we are a whale) AND the weightedAnonLoss is not tolerable.
		while ((winner.Sum(x => x.Amount) > liquidityClue) && (winnerAnonLoss > MaxWeightedAnonLoss))
		{
			List<TCoin> bestReducedWinner = winner;
			var bestAnonLoss = winnerAnonLoss;
			bool winnerchanged = false;

			// We always want to keep the non-private coins.
			foreach (TCoin coin in winner.Except(new[] { selectedNonPrivateCoin }))
			{
				var reducedWinner = winner.Except(new[] { coin });
				var anonLoss = GetAnonLoss(reducedWinner);

				if (anonLoss <= bestAnonLoss)
				{
					bestAnonLoss = anonLoss;
					bestReducedWinner = reducedWinner.ToList();
					winnerchanged = true;
				}
			}

			if (!winnerchanged)
			{
				break;
			}

			winner = bestReducedWinner;
			winnerAnonLoss = bestAnonLoss;
		}

		if (winner.Count != finalCandidate.Count())
		{
			Logger.LogDebug($"Optimizing selection, removing coins coming from the same tx.");
			Logger.LogDebug($"{nameof(sameTxAllowance)}: {sameTxAllowance}.");
			Logger.LogDebug($"{nameof(winner)}: {winner.Count} coins, {string.Join(", ", winner.Select(x => x.Amount.ToString(false, true)).ToArray())} BTC.");
		}

		if (winner.Count < MaxInputsRegistrableByWallet)
		{
			// If the address of a winner contains other coins (address reuse, same HdPubKey) that are available but not selected,
			// complete the selection with them until MaxInputsRegistrableByWallet threshold.
			// Order by most to least reused to try not splitting coins from same address into several rounds.
			var nonSelectedCoinsOnSameAddresses = filteredCoins
				.Except(winner)
				.Where(x => winner.Any(y => y.ScriptPubKey == x.ScriptPubKey))
				.GroupBy(x => x.ScriptPubKey)
				.OrderByDescending(g => g.Count())
				.SelectMany(g => g)
				.Take(MaxInputsRegistrableByWallet - winner.Count)
				.ToList();

			winner.AddRange(nonSelectedCoinsOnSameAddresses);

			if (nonSelectedCoinsOnSameAddresses.Count > 0)
			{
				Logger.LogInfo($"{nonSelectedCoinsOnSameAddresses.Count} coins were added to the selection because they are on the same addresses of some selected coins.");
			}
		}

		return winner.ToShuffled().ToImmutableList();
	}

	private static double GetAnonLoss<TCoin>(IEnumerable<TCoin> coins)
		where TCoin : ISmartCoin
	{
		double minimumAnonScore = coins.Min(x => x.AnonymitySet);
		return coins.Sum(x => (x.AnonymitySet - minimumAnonScore) * x.Amount.Satoshi) / coins.Sum(x => x.Amount.Satoshi);
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

	private static IEnumerable<TCoin> AnonScoreTxSourceBiasedShuffle<TCoin>(TCoin[] coins)
		where TCoin : ISmartCoin
	{
		var orderedCoins = new List<TCoin>();
		for (int i = 0; i < coins.Length; i++)
		{
			// Order by anonscore first.
			var remaining = coins.Except(orderedCoins).OrderBy(x => x.AnonymitySet);

			// Then manipulate the list so repeating tx sources go to the end.
			var alternating = new List<TCoin>();
			var skipped = new List<TCoin>();
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

	private static bool TryAddGroup<TCoin>(UtxoSelectionParameters parameters, Dictionary<int, IEnumerable<TCoin>> groups, IEnumerable<TCoin> group)
		where TCoin : ISmartCoin
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

	private static int GetReps<TCoin>(IEnumerable<TCoin> group)
		where TCoin : ISmartCoin
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
		var roundState = await RoundStatusUpdater.CreateRoundAwaiterAsync(roundId, Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
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

		// Get the output's size and its of the input that will spend it in the future.
		// Here we assume all the outputs share the same scriptpubkey type.
		var isTaprootAllowed = roundParameters.AllowedOutputTypes.Contains(ScriptType.Taproot);
		var preferTaprootOutputs = isTaprootAllowed && Random.Shared.NextDouble() < .5;
		var (inputVirtualSize, outputVirtualSize) = DestinationProvider.Peek(preferTaprootOutputs).IsScriptType(ScriptType.Taproot)
			? (Constants.P2trInputVirtualSize, Constants.P2trOutputVirtualSize)
			: (Constants.P2wpkhInputVirtualSize, Constants.P2wpkhOutputVirtualSize);

		AmountDecomposer amountDecomposer = new(roundParameters.MiningFeeRate, roundParameters.AllowedOutputAmounts, outputVirtualSize, inputVirtualSize, (int)availableVsize);
		var theirCoins = constructionState.Inputs.Where(x => !registeredCoins.Any(y => x.Outpoint == y.Outpoint));
		var registeredCoinEffectiveValues = registeredAliceClients.Select(x => x.EffectiveValue);
		var theirCoinEffectiveValues = theirCoins.Select(x => x.EffectiveValue(roundParameters.MiningFeeRate, roundParameters.CoordinationFeeRate));
		var outputValues = amountDecomposer.Decompose(registeredCoinEffectiveValues, theirCoinEffectiveValues);

		// Get as many destinations as outputs we need.
		var destinations = DestinationProvider.GetNextDestinations(outputValues.Count(), preferTaprootOutputs).ToArray();
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

	private async Task<(Transaction UnsignedCoinJoin, ImmutableArray<AliceClient> AliceClientsThatSigned)> ProceedWithSigningStateAsync(
		uint256 roundId,
		ImmutableArray<AliceClient> registeredAliceClients,
		IEnumerable<TxOut> outputTxOuts,
		CancellationToken cancellationToken)
	{
		// Signing.
		var roundState = await RoundStatusUpdater.CreateRoundAwaiterAsync(roundId, Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
		var remainingTime = roundState.CoinjoinState.Parameters.TransactionSigningTimeout - RoundStatusUpdater.Period;
		var signingStateEndTime = DateTimeOffset.UtcNow + remainingTime;

		CoinJoinClientProgress.SafeInvoke(this, new EnteringSigningPhase(roundState, signingStateEndTime));

		using CancellationTokenSource phaseTimeoutCts = new(remainingTime + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, phaseTimeoutCts.Token);

		roundState.LogDebug($"Transaction signing phase started - it will end in: {signingStateEndTime - DateTimeOffset.UtcNow:hh\\:mm\\:ss}.");

		var signingState = roundState.Assert<SigningState>();
		var unsignedCoinJoin = signingState.CreateUnsignedTransactionWithPrecomputedData();

		// If everything is okay, then sign all the inputs. Otherwise, in case there are missing outputs, the server is
		// lying (it lied us before when it responded with 200 OK to the OutputRegistration requests or it is lying us
		// now when we identify as satoshi.
		// In this scenario we should ban the coordinator and stop dealing with it.
		// see more: https://github.com/zkSNACKs/WalletWasabi/issues/8171
		bool mustSignAllInputs = SanityCheck(outputTxOuts, unsignedCoinJoin.Transaction.Outputs);
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

		return (unsignedCoinJoin.Transaction, alicesToSign);
	}

	private async Task<ImmutableArray<(AliceClient, PersonCircuit)>> ProceedWithInputRegAndConfirmAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		// Because of the nature of the protocol, the input registration and the connection confirmation phases are done subsequently thus they have a merged timeout.
		var timeUntilOutputReg = (roundState.InputRegistrationEnd - DateTimeOffset.UtcNow) + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;

		using CancellationTokenSource timeUntilOutputRegCts = new(timeUntilOutputReg + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeUntilOutputRegCts.Token);

		CoinJoinClientProgress.SafeInvoke(this, new EnteringInputRegistrationPhase(roundState, roundState.InputRegistrationEnd));

		// Register coins.
		var result = await CreateRegisterAndConfirmCoinsAsync(smartCoins, roundState, cancellationToken).ConfigureAwait(false);

		if (!RoundStatusUpdater.TryGetRoundState(roundState.Id, out var newRoundState))
		{
			throw new InvalidOperationException($"Round '{roundState.Id}' is missing.");
		}

		if (!result.IsDefaultOrEmpty)
		{
			// Be aware: at this point we are already in connection confirmation and all the coins got their first confirmation, so this is not exactly the starting time of the phase.
			var estimatedRemainingFromConnectionConfirmation = DateTimeOffset.UtcNow + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;
			CoinJoinClientProgress.SafeInvoke(this, new EnteringConnectionConfirmationPhase(newRoundState, estimatedRemainingFromConnectionConfirmation));
		}

		return result;
	}
}
