using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
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
		Kitchen kitchen,
		KeyManager keymanager,
		RoundStateUpdater roundStatusUpdater,
		int minAnonScoreTarget = int.MaxValue,
		bool consolidationMode = false)
	{
		HttpClientFactory = httpClientFactory;
		Kitchen = kitchen;
		Keymanager = keymanager;
		RoundStatusUpdater = roundStatusUpdater;
		MinAnonScoreTarget = minAnonScoreTarget;
		ConsolidationMode = consolidationMode;
		SecureRandom = new SecureRandom();
	}

	private SecureRandom SecureRandom { get; }
	public IWasabiHttpClientFactory HttpClientFactory { get; }
	public Kitchen Kitchen { get; }
	public KeyManager Keymanager { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	public int MinAnonScoreTarget { get; }

	public bool InCriticalCoinJoinState
	{
		get => _inCriticalCoinJoinState;
		private set => _inCriticalCoinJoinState = value;
	}

	public bool ConsolidationMode { get; private set; }

	public async Task<bool> StartCoinJoinAsync(IEnumerable<SmartCoin> coins, CancellationToken cancellationToken)
	{
		var currentRoundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cancellationToken).ConfigureAwait(false);

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
				.CreateRoundAwaiter(roundState => roundState.BlameOf == currentRoundState.Id && roundState.Phase == Phase.InputRegistration, linkedTokenSource.Token)
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

		var coinCandidates = SelectCoinsForRound(smartCoins, constructionState.Parameters);

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
			var availableVsize = registeredAliceClients.SelectMany(x => x.IssuedVsizeCredentials).Sum(x => x.Value);

			// Calculate outputs values
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(rs => rs.Id == roundState.Id, cancellationToken).ConfigureAwait(false);
			constructionState = roundState.Assert<ConstructionState>();
			AmountDecomposer amountDecomposer = new(roundState.FeeRate, roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min, Constants.P2wpkhOutputSizeInBytes, (int)availableVsize);
			var theirCoins = constructionState.Inputs.Except(registeredCoins);
			var outputValues = amountDecomposer.Decompose(registeredCoins, theirCoins);

			// Get all locked internal keys we have and assert we have enough.
			Keymanager.AssertLockedInternalKeysIndexed(howMany: outputValues.Count());
			var allLockedInternalKeys = Keymanager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked);
			var outputTxOuts = outputValues.Zip(allLockedInternalKeys, (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));

			DependencyGraph dependencyGraph = DependencyGraph.ResolveCredentialDependencies(registeredCoins, outputTxOuts, roundState.FeeRate, roundState.MaxVsizeAllocationPerAlice);
			DependencyGraphTaskScheduler scheduler = new(dependencyGraph);

			// Re-issuances.
			var bobClient = CreateBobClient(roundState);
			Logger.LogInfo($"Round ({roundState.Id}), Starting reissuances.");
			await scheduler.StartReissuancesAsync(registeredAliceClients, bobClient, cancellationToken).ConfigureAwait(false);

			// Output registration.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Output registration phase started.");

			await scheduler.StartOutputRegistrationsAsync(outputTxOuts, bobClient, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Outputs({outputTxOuts.Count()}) successfully registered.");

			// ReadyToSign.
			await ReadyToSignAsync(registeredAliceClients, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Alices({registeredAliceClients.Length}) successfully signalled ready to sign.");

			// Signing.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
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
			Logger.LogDebug($"Round ({roundState.Id}): Alices({registeredAliceClients.Length}) successfully signed the CoinJoin tx.");

			var finalRoundState = await RoundStatusUpdater.CreateRoundAwaiter(s => s.Id == roundState.Id && s.Phase == Phase.Ended, cancellationToken).ConfigureAwait(false);
			Logger.LogDebug($"Round ({roundState.Id}): Round Ended - WasTransactionBroadcast: '{finalRoundState.WasTransactionBroadcast}'.");

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

				var hdKey = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey).Single();
				var secret = hdKey.PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
				if (hdKey.PrivateKey.PubKey.WitHash.ScriptPubKey != coin.ScriptPubKey)
				{
					throw new InvalidOperationException("The key cannot generate the utxo scriptpubkey. This could happen if the wallet password is not the correct one.");
				}

				var masterKey = Keymanager.GetMasterExtKey(Kitchen.SaltSoup()).PrivateKey;
				var identificationMasterKey = Slip21Node.FromSeed(masterKey.ToBytes());
				var identificationKey = identificationMasterKey.DeriveChild("SLIP-0019").DeriveChild("Ownership identification key").Key;

				return await AliceClient.CreateRegisterAndConfirmInputAsync(roundState, aliceArenaClient, coin, secret, identificationKey, RoundStatusUpdater, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException)
			{
				return null;
			}
		}

		// Gets the list of scheduled dates/time in the remaining available time frame when each alice has to be registered.
		var remainingTimeForRegistration = (roundState.InputRegistrationEnd - DateTimeOffset.UtcNow) - TimeSpan.FromSeconds(15);

		Logger.LogDebug($"Round ({roundState.Id}): Input registration started, it will end in {remainingTimeForRegistration.TotalMinutes} minutes.");

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
		var expectedOutputTuples = expectedOutputs.Select(o => (o.Value, o.ScriptPubKey));
		return coinJoinOutputs.IsSuperSetOf(expectedOutputTuples);
	}

	private async Task SignTransactionAsync(IEnumerable<AliceClient> aliceClients, Transaction unsignedCoinJoinTransaction, RoundState roundState, CancellationToken cancellationToken)
	{
		async Task<AliceClient?> SignTransactionAsync(AliceClient aliceClient, CancellationToken cancellationToken)
		{
			try
			{
				await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, cancellationToken).ConfigureAwait(false);
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
		Logger.LogDebug($"Round ({roundState.Id}): Signing phase started, it will end in {transactionSigningTimeFrame.TotalMinutes} minutes.");

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

	private ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> coins, MultipartyTransactionParameters parameters)
	{
		var filteredCoins = coins
			.Where(x => parameters.AllowedInputAmounts.Contains(x.Amount))
			.Where(x => parameters.AllowedInputTypes.Any(t => x.ScriptPubKey.IsScriptType(t)))
			.ToShuffled() // Preshuffle before ordering.
			.OrderBy(x => x.HdPubKey.AnonymitySet)
			.ThenByDescending(y => y.Amount)
			.ToArray();

		// How many inputs do we want to provide to the mix?
		int inputCount = ConsolidationMode ? MaxInputsRegistrableByWallet : GetInputTarget(filteredCoins.Length);

		// Select a group of coins those are close to each other by Anonimity Score.
		List<IEnumerable<SmartCoin>> groups = new();

		// I can take more coins those are already reached the minimum privacy threshold.
		int nonPrivateCoinCount = filteredCoins.Where(x => x.HdPubKey.AnonymitySet < MinAnonScoreTarget).Count();
		for (int i = 0; i < nonPrivateCoinCount; i++)
		{
			// Make sure the group can at least register an output even after paying fees.
			var group = filteredCoins.Skip(i).Take(inputCount);

			if (group.Count() < Math.Min(filteredCoins.Length, inputCount))
			{
				break;
			}

			var inSum = group.Sum(x => x.EffectiveValue(parameters.FeeRate));
			var outFee = parameters.FeeRate.GetFee(Constants.P2wpkhOutputSizeInBytes);
			if (inSum >= outFee + parameters.AllowedOutputAmounts.Min)
			{
				groups.Add(group);
			}
		}

		// If there're no selections then there's no reason to mix.
		if (!groups.Any())
		{
			throw new InvalidOperationException("Coin selection failed to return a valid coin set.");
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

		var bestCost = anonScoreCosts.Select(g => g.Cost).Min();
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

	/// <summary>
	/// Calculates how many inputs are desirable to be registered
	/// based on rougly the total number of coins in a wallet.
	/// Note: random biasing is applied.
	/// </summary>
	/// <returns>Desired input count.</returns>
	private int GetInputTarget(int utxoCount)
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
			if (SecureRandom.GetInt(0, 10) < 5)
			{
				return best.Key;
			}
		}

		return targetInputCount;
	}
}
