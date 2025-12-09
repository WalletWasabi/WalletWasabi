using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class CoinJoinClient
{
	private static readonly Money MinimumOutputAmountSanity = Money.Coins(0.0001m); // ignore rounds with too big minimum denominations
	private static readonly TimeSpan ExtraPhaseTimeoutMargin = TimeSpan.FromMinutes(2);
	private static readonly TimeSpan ExtraRoundTimeoutMargin = TimeSpan.FromMinutes(10);

	// Maximum delay when spreading the requests in time, except input registration requests which
	// timings only depends on the input-reg timeout and signing requests which timings must be larger.
	// This is a maximum cap the delay can be smaller if the remaining time is less.
	private static readonly TimeSpan MaximumRequestDelay = TimeSpan.FromSeconds(10);

	public CoinJoinClient(
		Func<string, IWabiSabiApiRequestHandler> arenaRequestHandlerFactory,
		IKeyChain keyChain,
		OutputProvider outputProvider,
		RoundStateProvider roundStatusProvider,
		CoinJoinCoinSelector coinJoinCoinSelector,
		CoinJoinConfiguration coinJoinConfiguration,
		LiquidityClueProvider liquidityClueProvider)
	{
		ArenaRequestHandlerFactory = arenaRequestHandlerFactory;
		_keyChain = keyChain;
		_outputProvider = outputProvider;
		_roundStatusProvider = roundStatusProvider;
		_liquidityClueProvider = liquidityClueProvider;
		_coinJoinConfiguration = coinJoinConfiguration;
		_coinJoinCoinSelector = coinJoinCoinSelector;
		_safetyMarginForRegistration = TimeSpan.FromMinutes(1);
	}

	public event EventHandler<CoinJoinProgressEventArgs>? CoinJoinClientProgress;

	public ImmutableList<SmartCoin> CoinsInCriticalPhase { get; private set; } = ImmutableList<SmartCoin>.Empty;

	private Func<string, IWabiSabiApiRequestHandler> ArenaRequestHandlerFactory { get; }
	private readonly IKeyChain _keyChain;
	private readonly OutputProvider _outputProvider;
	private readonly RoundStateProvider _roundStatusProvider;
	private readonly LiquidityClueProvider _liquidityClueProvider;
	private readonly CoinJoinConfiguration _coinJoinConfiguration;
	private readonly CoinJoinCoinSelector _coinJoinCoinSelector;
	private readonly TimeSpan _maxWaitingTimeForRound = TimeSpan.FromMinutes(10);
	protected TimeSpan _safetyMarginForRegistration;
	private async Task<RoundState> WaitForRoundAsync(uint256 excludeRound, CancellationToken token)
	{
		CoinJoinClientProgress.SafeInvoke(this, new WaitingForRound());

		using CancellationTokenSource cts = new(_maxWaitingTimeForRound);
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, cts.Token);

		return await _roundStatusProvider
			.CreateRoundAwaiterAsync(
				roundState =>
					roundState.InputRegistrationEnd - DateTimeOffset.UtcNow >  TimeSpan.FromMicroseconds(1)
					&& roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min < MinimumOutputAmountSanity
					&& roundState.Phase == Phase.InputRegistration
					&& !roundState.IsBlame
					&& roundState.Id != excludeRound,
				linkedCts.Token)
			.ConfigureAwait(false);
	}

	private async Task<RoundState> WaitForBlameRoundAsync(uint256 blameRoundId, CancellationToken token)
	{
		var timeout = TimeSpan.FromMinutes(5);
		CoinJoinClientProgress.SafeInvoke(this, new WaitingForBlameRound(DateTimeOffset.UtcNow + timeout));

		using CancellationTokenSource waitForBlameRoundCts = new(timeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(waitForBlameRoundCts.Token, token);

		var roundState = await _roundStatusProvider
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

		if (roundState.CoinjoinState.Parameters.MinInputCountByRound < _coinJoinConfiguration.AbsoluteMinInputCount)
		{
			throw new InvalidOperationException($"Blame Round ({roundState.Id}): Abandoning: the minimum input count was too low.");
		}

		if (roundState.CoinjoinState.Parameters.MiningFeeRate.SatoshiPerByte > _coinJoinConfiguration.MaxCoinJoinMiningFeeRate)
		{
			throw new InvalidOperationException($"Blame Round ({roundState.Id}): Abandoning: " +
												$"the mining fee rate for the round was {roundState.CoinjoinState.Parameters.MiningFeeRate.SatoshiPerByte} sat/vb but maximum allowed is {_coinJoinConfiguration.MaxCoinJoinMiningFeeRate}.");
		}

		return roundState;
	}

	public async Task<CoinJoinResult> StartCoinJoinAsync(Func<Task<IEnumerable<SmartCoin>>> coinCandidatesFunc, bool stopWhenAllMixed, CancellationToken cancellationToken)
	{
		RoundState? currentRoundState;
		uint256 excludeRound = uint256.Zero;
		ImmutableList<SmartCoin> coins;
		IEnumerable<SmartCoin> coinCandidates;

		do
		{
			// Sanity check if we would get coins at all otherwise this will throw.
			await coinCandidatesFunc().ConfigureAwait(false);

			currentRoundState = await WaitForRoundAsync(excludeRound, cancellationToken).ConfigureAwait(false);
			RoundParameters roundParameters = currentRoundState.CoinjoinState.Parameters;

			if (!currentRoundState.IsBlame)
			{
				if (roundParameters.MiningFeeRate.SatoshiPerByte > _coinJoinConfiguration.MaxCoinJoinMiningFeeRate)
				{
					string roundSkippedMessage = $"Mining fee rate was {roundParameters.MiningFeeRate} but max allowed is {_coinJoinConfiguration.MaxCoinJoinMiningFeeRate}.";
					currentRoundState.LogInfo(roundSkippedMessage);
					throw new CoinJoinClientException(CoinjoinError.MiningFeeRateTooHigh, roundSkippedMessage);
				}
				if (roundParameters.MinInputCountByRound < _coinJoinConfiguration.AbsoluteMinInputCount)
				{
					string roundSkippedMessage = $"Min input count for the round was {roundParameters.MinInputCountByRound} but min allowed is {_coinJoinConfiguration.AbsoluteMinInputCount}.";
					currentRoundState.LogInfo(roundSkippedMessage);
					throw new CoinJoinClientException(CoinjoinError.MinInputCountTooLow, roundSkippedMessage);
				}
			}

			coinCandidates = await coinCandidatesFunc().ConfigureAwait(false);

			var liquidityClue = _liquidityClueProvider.GetLiquidityClue(roundParameters.MaxSuggestedAmount);
			var utxoSelectionParameters = UtxoSelectionParameters.FromRoundParameters(roundParameters, _outputProvider.DestinationProvider.SupportedScriptTypes.ToArray());

			coins = _coinJoinCoinSelector.SelectCoinsForRound(coinCandidates, utxoSelectionParameters, liquidityClue);

			if (!roundParameters.AllowedInputTypes.Contains(ScriptType.P2WPKH) || !roundParameters.AllowedOutputTypes.Contains(ScriptType.P2WPKH))
			{
				excludeRound = currentRoundState.Id;
				currentRoundState.LogInfo("Skipping the round since it doesn't support P2WPKH inputs and outputs.");

				continue;
			}

			if (roundParameters.MaxSuggestedAmount != default && coins.Any(c => c.Amount > roundParameters.MaxSuggestedAmount))
			{
				excludeRound = currentRoundState.Id;
				currentRoundState.LogInfo($"Skipping the round for more optimal mixing. Max suggested amount is '{roundParameters.MaxSuggestedAmount}' BTC, biggest coin amount is: '{coins.Select(c => c.Amount).Max()}' BTC.");

				continue;
			}

			break;
		}
		while (!cancellationToken.IsCancellationRequested);

		if (coins.IsEmpty)
		{
			throw new CoinJoinClientException(CoinjoinError.NoCoinsEligibleToMix, $"No coin was selected from '{coinCandidates.Count()}' number of coins. Probably it was not economical, total amount of coins were: {Money.Satoshis(coinCandidates.Sum(c => c.Amount))} BTC.");
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
			var rs = await _roundStatusProvider.CreateRoundAwaiterAsync(roundId, Phase.Ended, linkedCts.Token).ConfigureAwait(false);

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

			var signedCoins = aliceClientsThatSigned.Select(a => a.SmartCoin).ToImmutableList();

			try
			{
				roundState = await waitRoundEndedTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				roundState.Log(LogLevel.Warning, $"Waiting for the round to end failed with: '{ex}'.");
				throw new UnknownRoundEndingException(signedCoins, outputTxOuts.Select(o => o.ScriptPubKey).ToImmutableList(), ex);
			}

			var hash = unsignedCoinJoin is { } tx ? tx.GetHash().ToString() : "Not available";

			var msg = roundState.EndRoundState switch
			{
				EndRoundState.TransactionBroadcasted => $"Broadcast. Coinjoin TxId: ({hash})",
				EndRoundState.TransactionBroadcastFailed => $"Failed to broadcast. Coinjoin TxId: ({hash})",
				EndRoundState.AbortedWithError => "Round abnormally finished.",
				EndRoundState.AbortedNotEnoughAlices => "Aborted. Not enough participants.",
				EndRoundState.AbortedNotEnoughAlicesSigned => "Aborted. Not enough participants signed the coinjoin transaction.",
				EndRoundState.NotAllAlicesSign => "Aborted. Some Alices didn't sign. Go to blame round.",
				EndRoundState.AbortedNotAllAlicesConfirmed => "Aborted. Some Alices didn't confirm.",
				EndRoundState.AbortedLoadBalancing => "Aborted. Load balancing registrations.",
				EndRoundState.None => "Unknown.",
				_ => throw new ArgumentOutOfRangeException(nameof(roundState))
			};

			roundState.LogInfo(msg);

			// Coinjoin succeeded but wallet had no input in it.
			if (signedCoins.IsEmpty && roundState.EndRoundState == EndRoundState.TransactionBroadcasted)
			{
				throw new CoinJoinClientException(CoinjoinError.UserWasntInRound, "No inputs participated in this round.");
			}

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
				await waitRoundEndedTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// Make sure that to not generate UnobservedTaskException.
				roundState.LogDebug(ex.Message);
			}

			CoinJoinClientProgress.SafeInvoke(this, new LeavingCriticalPhase());
			CoinJoinClientProgress.SafeInvoke(this, new RoundEnded(roundState));
		}
	}

	private async Task<(ImmutableArray<AliceClient> aliceClientsThatSigned, IEnumerable<TxOut> OutputTxOuts, Transaction UnsignedCoinJoin)> ProceedWithRoundAsync(RoundState roundState, IEnumerable<SmartCoin> smartCoins, CancellationToken cancellationToken)
	{
		var registeredAliceClients = ImmutableArray<AliceClient>.Empty;
		try
		{
			var roundId = roundState.Id;
			roundState.LogInfo("Started.");
			roundState.LogInfo($"FeeRate: {roundState.CoinjoinState.Parameters.MiningFeeRate} MaxAllowedMiningFeeRate: {_coinJoinConfiguration.MaxCoinJoinMiningFeeRate}");

			registeredAliceClients = await ProceedWithInputRegAndConfirmAsync(smartCoins, roundState, cancellationToken).ConfigureAwait(false);
			if (!registeredAliceClients.Any())
			{
				throw new CoinJoinClientException(CoinjoinError.CoinsRejected, $"The coordinator rejected all {smartCoins.Count()} inputs.");
			}

			roundState.LogInfo($"Successfully registered {registeredAliceClients.Length} inputs.");

			CoinsInCriticalPhase = registeredAliceClients.Select(alice => alice.SmartCoin).ToImmutableList();

			var outputTxOuts = await ProceedWithOutputRegistrationPhaseAsync(roundId, registeredAliceClients, cancellationToken).ConfigureAwait(false);

			var (unsignedCoinJoin, aliceClientsThatSigned) = await ProceedWithSigningStateAsync(roundId, registeredAliceClients, outputTxOuts, cancellationToken).ConfigureAwait(false);
			LogCoinJoinSummary(registeredAliceClients, outputTxOuts, roundState);

			_liquidityClueProvider.UpdateLiquidityClue(roundState.CoinjoinState.Parameters.MaxSuggestedAmount, unsignedCoinJoin, outputTxOuts);

			return (aliceClientsThatSigned, outputTxOuts, unsignedCoinJoin);
		}
		finally
		{
			foreach (var coins in smartCoins)
			{
				coins.CoinJoinInProgress = false;
			}

			foreach (var aliceClientAndCircuit in registeredAliceClients)
			{
				aliceClientAndCircuit.Finish();
			}
		}
	}

	private async Task<ImmutableArray<AliceClient>> CreateRegisterAndConfirmCoinsAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancel)
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

		async Task<AliceClient?> RegisterInputAsync(SmartCoin coin)
		{
			try
			{
				var aliceArenaClient = new ArenaClient(
					roundState.CreateAmountCredentialClient(SecureRandom.Instance),
					roundState.CreateVsizeCredentialClient(SecureRandom.Instance),
					_coinJoinConfiguration.CoordinatorIdentifier,
					ArenaRequestHandlerFactory($"alice-{coin.Outpoint}"));

				var aliceClient = await AliceClient.CreateRegisterAndConfirmInputAsync(roundState, aliceArenaClient, coin, _keyChain, _roundStatusProvider, linkedUnregisterCts.Token, linkedRegistrationsCts.Token, linkedConfirmationsCts.Token).ConfigureAwait(false);

				// Right after the first real-cred confirmation happened we entered into critical phase.
				if (Interlocked.Exchange(ref eventInvokedAlready, 1) == 0)
				{
					CoinJoinClientProgress.SafeInvoke(this, new EnteringCriticalPhase());
				}

				return aliceClient;
			}
			catch (WabiSabiProtocolException wpe)
			{
				switch (wpe.ErrorCode)
				{
					case WabiSabiProtocolErrorCode.RoundNotFound:
						// if the round does not exist then it ended/aborted.
						roundState.LogInfo($"{coin.Coin.Outpoint} arrived too late because the round doesn't exist anymore. Aborting input registrations: '{WabiSabiProtocolErrorCode.RoundNotFound}'.");
						registrationsCts.Cancel();
						confirmationsCts.Cancel();
						break;

					case WabiSabiProtocolErrorCode.WrongPhase:
						if (wpe.ExceptionData is WrongPhaseExceptionData wrongPhaseExceptionData)
						{
							roundState.LogInfo($"{coin.Coin.Outpoint} arrived too late. Aborting input registrations: '{WabiSabiProtocolErrorCode.WrongPhase}'.");
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
						break;

					case WabiSabiProtocolErrorCode.AliceAlreadyRegistered:
						roundState.LogInfo($"{coin.Coin.Outpoint} was already registered.");
						break;

					case WabiSabiProtocolErrorCode.AliceAlreadyConfirmedConnection:
						roundState.LogInfo($"{coin.Coin.Outpoint} already confirmed connection.");
						break;

					case WabiSabiProtocolErrorCode.InputSpent:
						coin.SpentAccordingToNetwork = true;
						roundState.LogInfo($"{coin.Coin.Outpoint} is spent according to the backend. The wallet is not fully synchronized or corrupted.");
						break;

					case WabiSabiProtocolErrorCode.InputBanned or WabiSabiProtocolErrorCode.InputLongBanned:
						var inputBannedExData = wpe.ExceptionData as InputBannedExceptionData;
						if (inputBannedExData is null)
						{
							Logger.LogError($"{nameof(InputBannedExceptionData)} is missing.");
						}
						var bannedUntil = inputBannedExData?.BannedUntil ?? DateTimeOffset.UtcNow + TimeSpan.FromDays(1);
						CoinJoinClientProgress.SafeInvoke(this, new CoinBanned(coin, bannedUntil));
						roundState.LogInfo($"{coin.Coin.Outpoint} is banned until {bannedUntil}.");
						break;

					case WabiSabiProtocolErrorCode.InputNotWhitelisted:
						coin.SpentAccordingToNetwork = false;
						Logger.LogWarning($"{coin.Coin.Outpoint} cannot be registered in the blame round.");
						break;

					default:
						roundState.LogInfo($"{coin.Coin.Outpoint} cannot be registered: '{wpe.ErrorCode}'.");
						break;
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

			// In case of any exception.
			return null;
		}

		// Gets the list of scheduled dates/time in the remaining available time frame when each alice has to be registered.
		var remainingTimeForRegistration = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow;

		roundState.LogDebug($"Inputs({smartCoins.Count()}) registration started - it will end in: {remainingTimeForRegistration:hh\\:mm\\:ss}.");

		// Decrease the available time, so the clients hurry up.
		var safetyBuffer = TimeSpan.FromMinutes(1);
		var remainingTime = roundState.InputRegistrationEnd - safetyBuffer;
		var scheduledDates = remainingTime.GetScheduledDates(smartCoins.Count());

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
			.Where(r => r is not null)
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
		var identity = Convert.ToHexString(SecureRandom.Instance.GetBytes(20)).ToLower();
		return new BobClient(
			roundState.Id,
			new(
				roundState.CreateAmountCredentialClient(SecureRandom.Instance),
				roundState.CreateVsizeCredentialClient(SecureRandom.Instance),
				_coinJoinConfiguration.CoordinatorIdentifier,
				ArenaRequestHandlerFactory($"bob-{identity}")));
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
				.All(x => x >= Money.Zero);

		return AllExpectedScriptsArePresent() && AllOutputsHaveAtLeastTheExpectedValue();
	}

	private async Task SignTransactionAsync(
		IEnumerable<AliceClient> aliceClients,
		TransactionWithPrecomputedData unsignedCoinJoinTransaction,
		DateTimeOffset signingStartTime,
		DateTimeOffset signingEndTime,
		CancellationToken cancellationToken)
	{
		// Maximum signing request delay is 50 seconds, because
		// - the fast track signing phase will be 1m 30s, so we want to give a decent time for the requests to be sent out.
		var maximumSigningRequestDelay = TimeSpan.FromSeconds(50);
		var scheduledDates = signingEndTime.GetScheduledDates(aliceClients.Count(), signingStartTime, maximumSigningRequestDelay);

		var tasks = aliceClients.Zip(
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
					await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, _keyChain, cancellationToken).ConfigureAwait(false);
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
		var scheduledDates = GetScheduledDates(aliceClients.Count(), DateTimeOffset.UtcNow, readyToSignEndTime, MaximumRequestDelay);

		var tasks = aliceClients.Zip(
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

	internal virtual ImmutableList<DateTimeOffset> GetScheduledDates(int howMany, DateTimeOffset startTime, DateTimeOffset endTime, TimeSpan maximumRequestDelay)
	{
		return endTime.GetScheduledDates(howMany, startTime, maximumRequestDelay);
	}

	private void LogCoinJoinSummary(ImmutableArray<AliceClient> registeredAliceClients, IEnumerable<TxOut> myOutputs, RoundState roundState)
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

		string[] summary = new string[]
		{
			"",
			$"\tInput total : {totalInputAmount.ToString(true, false)} Eff: {totalEffectiveInputAmount.ToString(true, false)} NetworkFee: {inputNetworkFee.ToString(true, false)}",
			$"\tOutput total: {totalOutputAmount.ToString(true, false)} Eff: {totalEffectiveOutputAmount.ToString(true, false)} NetworkFee: {outputNetworkFee.ToString(true, false)}",
			$"\tTotal diff  : {totalDifference.ToString(true, false)}",
			$"\tEffect diff : {effectiveDifference.ToString(true, false)}",
			$"\tTotal fee   : {totalNetworkFee.ToString(true, false)}"
		};

		roundState.LogDebug(string.Join(Environment.NewLine, summary));
	}

	private async Task<IEnumerable<TxOut>> ProceedWithOutputRegistrationPhaseAsync(uint256 roundId, ImmutableArray<AliceClient> registeredAliceClients, CancellationToken cancellationToken)
	{
		// Waiting for OutputRegistration phase, all the Alices confirmed their connections, so the list of the inputs will be complete.
		var roundState = await _roundStatusProvider.CreateRoundAwaiterAsync(roundId, Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
		var roundParameters = roundState.CoinjoinState.Parameters;
		var remainingTime = roundParameters.OutputRegistrationTimeout - RoundStateProvider.QueryFrequency;
		var now = DateTimeOffset.UtcNow;
		var outputRegistrationPhaseEndTime = now + remainingTime;

		// Splitting the remaining time.
		// Both operations are done under output registration phase, so we have to do the random timing taking that into account.
		var outputRegistrationEndTime = now + (remainingTime * 0.8); // 80% of the time.
		var readyToSignEndTime = outputRegistrationEndTime + (remainingTime * 0.2); // 20% of the time.

		CoinJoinClientProgress.SafeInvoke(this, new EnteringOutputRegistrationPhase(roundState, outputRegistrationPhaseEndTime));

		using CancellationTokenSource phaseTimeoutCts = new(remainingTime + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, phaseTimeoutCts.Token);

		var registeredCoins = registeredAliceClients.Select(x => x.SmartCoin.Coin);
		var availableVsizes = registeredAliceClients.SelectMany(x => x.IssuedVsizeCredentials.Where(y => y.Value > 0)).Select(x => x.Value);

		// Calculate outputs values
		var constructionState = roundState.Assert<ConstructionState>();

		var (ourCoins, theirCoins) = constructionState.Inputs.Partition(x => registeredCoins.Any(y => x.Outpoint == y.Outpoint));
		var registeredCoinEffectiveValues = registeredAliceClients.Select(x => x.EffectiveValue);
		var theirCoinEffectiveValues = theirCoins.Select(x => x.EffectiveValue(roundParameters.MiningFeeRate));

		// Verify that prevout information is correct for our own inputs.
		if (!registeredCoins.All(registeredCoin =>
			    ourCoins.Any(ourCoin =>
				    ourCoin.TxOut.ScriptPubKey.Hash == registeredCoin.TxOut.ScriptPubKey.Hash &&
				    ourCoin.TxOut.Value == registeredCoin.TxOut.Value)))
		{
			throw new CoinJoinClientException(CoinjoinError.CoordinatorLiedAboutInputs, "Coordinator lied about registered inputs. It probably tries to be malicious.");
		}

		var outputTxOuts = _outputProvider.GetOutputs(roundId, roundParameters, registeredCoinEffectiveValues, theirCoinEffectiveValues, (int)availableVsizes.Sum()).ToArray();

		DependencyGraph dependencyGraph = DependencyGraph.ResolveCredentialDependencies(registeredCoinEffectiveValues, outputTxOuts, roundParameters.MiningFeeRate, availableVsizes, roundParameters.MaxAmountCredentialValue, roundParameters.MaxVsizeCredentialValue);
		DependencyGraphTaskScheduler scheduler = new(dependencyGraph);

		var combinedToken = linkedCts.Token;
		try
		{
			// Re-issuances.
			Func<BobClient> bobClientFactory = () => CreateBobClient(roundState);
			roundState.LogInfo("Starting reissuances.");
			await scheduler.StartReissuancesAsync(registeredAliceClients, bobClientFactory, combinedToken).ConfigureAwait(false);

			// Output registration.
			roundState.LogDebug($"Output registration started - it will end in: {outputRegistrationEndTime - DateTimeOffset.UtcNow:hh\\:mm\\:ss}.");

			var outputRegistrationScheduledDates = outputRegistrationEndTime.GetScheduledDates(outputTxOuts.Length, DateTimeOffset.UtcNow, MaximumRequestDelay);
			var registrationResult = await scheduler.StartOutputRegistrationsAsync(outputTxOuts, bobClientFactory, outputRegistrationScheduledDates, combinedToken).ConfigureAwait(false);
			registrationResult.MatchDo(
				OnOutputRegistrationSuccess,
				OnOutputRegistrationErrors);
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

		void OnOutputRegistrationSuccess(Unit _) =>
			roundState.LogInfo($"Outputs({outputTxOuts.Length}) were registered.");

		void OnOutputRegistrationErrors(DependencyGraphTaskScheduler.OutputRegistrationError[] errors)
		{
			foreach (var e in errors)
			{
				switch (e)
				{
					case DependencyGraphTaskScheduler.UnknownError s:
						roundState.LogInfo($"Script ({s.ScriptPubKey}) registration failed by unknown reasons. Continuing...");
						break;

					case DependencyGraphTaskScheduler.AlreadyRegisteredScriptError s:
						_outputProvider.DestinationProvider.TrySetScriptStates(KeyState.Used, [s.ScriptPubKey]);
						roundState.LogInfo($"Script ({s.ScriptPubKey}) was already registered. Continuing...");
						break;
				}
			}
		}
	}

	private async Task<(Transaction UnsignedCoinJoin, ImmutableArray<AliceClient> AliceClientsThatSigned)> ProceedWithSigningStateAsync(
		uint256 roundId,
		ImmutableArray<AliceClient> registeredAliceClients,
		IEnumerable<TxOut> outputTxOuts,
		CancellationToken cancellationToken)
	{
		// Signing.
		var roundState = await _roundStatusProvider.CreateRoundAwaiterAsync(roundId, Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
		var remainingTime = roundState.CoinjoinState.Parameters.TransactionSigningTimeout - TimeSpan.FromSeconds(10); // - _roundStatusProvider.Period; FIXME
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
		// see more: https://github.com/WalletWasabi/WalletWasabi/issues/8171
		var isItSoloCoinjoin = signingState.Inputs.Count() == registeredAliceClients.Length;
		var isItForbiddenSoloCoinjoining = isItSoloCoinjoin && !_coinJoinConfiguration.AllowSoloCoinjoining;
		if (isItForbiddenSoloCoinjoining)
		{
			roundState.LogInfo($"I am the only one in that coinjoin.");
		}
		bool allMyOutputsArePresent = SanityCheck(outputTxOuts, unsignedCoinJoin.Transaction.Outputs);

		if (!allMyOutputsArePresent)
		{
			roundState.LogInfo($"There are missing outputs.");
		}

		// Assert that the effective fee rate is at least what was agreed on.
		// Otherwise, coordinator could take some of the mining fees for itself.
		// There is a tolerance because before constructing the transaction only an estimation can be computed.
		var isCoordinatorTakingExtraFees = signingState.EffectiveFeeRate.FeePerK.Satoshi <= signingState.Parameters.MiningFeeRate.FeePerK.Satoshi * 0.90;
		if (isCoordinatorTakingExtraFees)
		{
			roundState.LogInfo($"Effective fee rate of the transaction is lower than expected.");
		}

		var mustSignAllInputs = !isItForbiddenSoloCoinjoining && allMyOutputsArePresent && !isCoordinatorTakingExtraFees;
		if (!mustSignAllInputs)
		{
			roundState.LogInfo($"A subset of inputs will be signed.	");
		}

		// Send signature.
		var combinedToken = linkedCts.Token;
		var alicesToSign = mustSignAllInputs
			? registeredAliceClients
			: registeredAliceClients.RemoveAt(SecureRandom.Instance.GetInt(0, registeredAliceClients.Length));

		var delayBeforeSigning = TimeSpan.FromSeconds(roundState.CoinjoinState.Parameters.DelayTransactionSigning ? 50 : 0);
		var signingStateStartTime = DateTimeOffset.UtcNow + delayBeforeSigning;
		await SignTransactionAsync(alicesToSign, unsignedCoinJoin, signingStateStartTime, signingStateEndTime, combinedToken).ConfigureAwait(false);
		roundState.LogInfo($"{alicesToSign.Length} out of {registeredAliceClients.Length} Alices have signed the coinjoin tx.");

		return (unsignedCoinJoin.Transaction, alicesToSign);
	}

	private async Task<ImmutableArray<AliceClient>> ProceedWithInputRegAndConfirmAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
	{
		// Because of the nature of the protocol, the input registration and the connection confirmation phases are done subsequently thus they have a merged timeout.
		var timeUntilOutputReg = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;

		using CancellationTokenSource timeUntilOutputRegCts = new(timeUntilOutputReg + ExtraPhaseTimeoutMargin);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeUntilOutputRegCts.Token);

		CoinJoinClientProgress.SafeInvoke(this, new EnteringInputRegistrationPhase(roundState, roundState.InputRegistrationEnd));

		// Register coins.
		var result = await CreateRegisterAndConfirmCoinsAsync(smartCoins, roundState, cancellationToken).ConfigureAwait(false);

		if (!result.IsDefaultOrEmpty)
		{
			// Be aware: at this point we are already in connection confirmation and all the coins got their first confirmation, so this is not exactly the starting time of the phase.
			var estimatedRemainingFromConnectionConfirmation = DateTimeOffset.UtcNow + roundState.CoinjoinState.Parameters.ConnectionConfirmationTimeout;
			CoinJoinClientProgress.SafeInvoke(this, new EnteringConnectionConfirmationPhase(roundState, estimatedRemainingFromConnectionConfirmation));
		}

		return result;
	}
}
