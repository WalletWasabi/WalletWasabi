using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Logging;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Coordinator.Statistics;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;
using static WalletWasabi.WabiSabi.Client.CoinJoin.Client.CoinJoinClient;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class WabiSabiHttpApiIntegrationTests : IClassFixture<WabiSabiApiApplicationFactory<Startup>>
{
	private readonly WabiSabiApiApplicationFactory<Startup> _apiApplicationFactory;
	private readonly ITestOutputHelper _output;

	public WabiSabiHttpApiIntegrationTests(WabiSabiApiApplicationFactory<Startup> apiApplicationFactory, ITestOutputHelper output)
	{
		_apiApplicationFactory = apiApplicationFactory;
		_output = output;
	}

	[Fact]
	public async Task RegisterSpentOrInNonExistentCoinAsync()
	{
		var httpClient = _apiApplicationFactory.CreateClient();
		await Task.Delay(100);
		var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
		var rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, CancellationToken.None)).RoundStates;
		var round = rounds.First(x => x.CoinjoinState is ConstructionState);

		// If an output is not in the utxo dataset then it is not unspent, this
		// means that the output is spent or simply doesn't even exist.
		var nonExistingOutPoint = new OutPoint();
		using var signingKey = new Key();
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () =>
		   await apiClient.RegisterInputAsync(round.Id, nonExistingOutPoint, ownershipProof, CancellationToken.None));

		Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);
	}

	[Fact]
	public async Task RegisterBannedCoinAsync()
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

		using var signingKey = new Key();
		var coin = WabiSabiFactory.CreateCoin(signingKey);
		var bannedOutPoint = coin.Outpoint;

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				var rpc = BitcoinFactory.GetMockMinimalRpc();

				// Make the coordinator believe that the coins are real and
				// that they exist in the blockchain with many confirmations.
				rpc.OnGetTxOutAsync = (_, _, _) => new()
				{
					Confirmations = 101,
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					TxOut = coin.TxOut
				};
				services.AddSingleton<IRPCClient>(s => rpc);

				var prison = WabiSabiFactory.CreatePrison();
				prison.FailedVerification(bannedOutPoint, uint256.One);
				services.AddSingleton(_ => prison);
			})).CreateClient();

		await Task.Delay(100);
		var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
		var rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, timeoutCts.Token)).RoundStates;
		var round = rounds.First(x => x.CoinjoinState is ConstructionState);

		// If an output is not in the utxo dataset then it is not unspent, this
		// means that the output is spent or simply doesn't even exist.
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () =>
			await apiClient.RegisterInputAsync(round.Id, bannedOutPoint, ownershipProof, timeoutCts.Token));

		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		var inputBannedData = Assert.IsType<InputBannedExceptionData>(ex.ExceptionData);
		Assert.True(inputBannedData.BannedUntil > DateTimeOffset.UtcNow);
	}

	[Theory]
	[InlineData(new long[] { 10_000_000, 20_000_000, 30_000_000, 40_000_000, 100_000_000 })]
	public async Task SoloCoinJoinTestAsync(long[] amounts)
	{
		int inputCount = amounts.Length;

		// At the end of the test a coinjoin transaction has to be created and broadcasted.
		var transactionCompleted = new TaskCompletionSource<Transaction>();

		// Create a key manager and use it to create fake coins.
		_output.WriteLine("Creating key manager...");
		KeyManager keyManager = KeyManager.CreateNew(out _, password: "", Network.Main);

		var coins = GenerateSmartCoins(keyManager, amounts, inputCount);

		_output.WriteLine("Coins were created successfully");

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.AddMockRpcClient(
				coins,
				rpc =>

					// Make the coordinator believe that the transaction is being
					// broadcasted using the RPC interface. Once we receive this tx
					// (the `SendRawTransactionAsync` was invoked) we stop waiting
					// and finish the waiting tasks to finish the test successfully.
					rpc.OnSendRawTransactionAsync = (tx) =>
					{
						transactionCompleted.SetResult(tx);
						return tx.GetHash();
					})
			.ConfigureServices(services =>
			{
				// Instruct the coordinator DI container to use these two scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddSingleton(s => new WabiSabiConfig
				{
					MaxInputCountByRound = inputCount - 1,  // Make sure that at least one IR fails for WrongPhase
					StandardInputRegistrationTimeout = TimeSpan.FromSeconds(20),
					ConnectionConfirmationTimeout = TimeSpan.FromSeconds(20),
					OutputRegistrationTimeout = TimeSpan.FromSeconds(20),
					TransactionSigningTimeout = TimeSpan.FromSeconds(20),
					MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
				});
			})).CreateClient();

		// Create the coinjoin client
		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));
		cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

		using var roundStateUpdater = RoundStateUpdaterForTesting.Create(apiClient);
		var roundStateProvider = new RoundStateProvider(roundStateUpdater);

		var coinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(_ => apiClient, keyManager, roundStateProvider);

		// Run the coinjoin client task.
		var coinjoinResult = await coinJoinClient.StartCoinJoinAsync(() => coins, cts.Token);
		Assert.True(coinjoinResult is SuccessfulCoinJoinResult);

		var broadcastedTx = await transactionCompleted.Task; // wait for the transaction to be broadcasted.
		Assert.NotNull(broadcastedTx);
	}

	[Fact]
	public async Task FailToRegisterOutputsCoinJoinTestAsync()
	{
		long[] amounts = [10_000_000, 20_000_000, 30_000_000];
		int inputCount = amounts.Length;

		// At the end of the test a coinjoin transaction has to be created and broadcasted.
		var transactionCompleted = new TaskCompletionSource<Transaction>();

		// Create a key manager and use it to create fake coins.
		_output.WriteLine("Creating key manager...");
		KeyManager keyManager = KeyManager.CreateNew(out _, password: "", Network.Main);

		var coins = GenerateSmartCoins(keyManager, amounts, inputCount);

		_output.WriteLine("Coins were created successfully");

		keyManager.AssertLockedInternalKeysIndexedAndPersist(21, false);
		keyManager.AssertLockedInternalKeysIndexedAndPersist(21, true);

		var keysCandidates = keyManager.GetNextCoinJoinKeys().ToArray();
		var outputScriptCandidates = keysCandidates
			.SelectMany(x => new[] {x.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit), x.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86)})
			.ToImmutableArray();

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder
			.AddMockRpcClient(coins, _ => { })
			.ConfigureServices(services =>
			{
				// Instruct the coordinator DI container to use this scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddSingleton(_ => new WabiSabiConfig
				{
					MaxInputCountByRound = inputCount,
					StandardInputRegistrationTimeout = TimeSpan.FromSeconds(20),
					ConnectionConfirmationTimeout = TimeSpan.FromSeconds(20),
					OutputRegistrationTimeout = TimeSpan.FromSeconds(20),
					TransactionSigningTimeout = TimeSpan.FromSeconds(20),
					MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
				});

				// Emulate that all our outputs had been already used in the past.
				// the server will prevent the registration and fail with a WabiSabiProtocolError.
				services.AddSingleton(_ => new CoinJoinScriptStore(outputScriptCandidates));
			})).CreateClient();

		// Create the coinjoin client
		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
		cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

		using var roundStateUpdater = RoundStateUpdaterForTesting.Create(apiClient, cts.Token);
		var roundStateProvider = new RoundStateProvider(roundStateUpdater);

		var coinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(_=> apiClient, keyManager, roundStateProvider);

		// Run the coinjoin client task.
		var coinjoinResultTask = coinJoinClient.StartCoinJoinAsync(() => coins, cts.Token);

		// If we see a blame round that means that the original round failed
		var blameRoundWaiterTask = roundStateProvider.CreateRoundAwaiterAsync(r => r.IsBlame, cts.Token);

		var finishedTask = await Task.WhenAny(coinjoinResultTask, blameRoundWaiterTask);
		if (finishedTask == coinjoinResultTask)
		{
			try
			{
				var coinjoinResult = await coinjoinResultTask;
				if (coinjoinResult is SuccessfulCoinJoinResult successfulCoinJoinResult)
				{
					var scripts = successfulCoinJoinResult.UnsignedCoinJoin.Outputs.Select(x => x.ScriptPubKey);
					var common = outputScriptCandidates.Intersect(scripts);
					Assert.Empty(common);
					throw new Exception("Coinjoin should have never finished successfully.");
				}
			}
			catch (InvalidOperationException e) when(e.Message.StartsWith("No valid output denominations found"))
			{
				// ignore. There is a rare case for this
			}
		}
		else
		{
			// Task.WhenAny does not propagate cancellation or failures from the winning task.
			// Await it so a timeout fails the test instead of passing without a blame round.
			await blameRoundWaiterTask;
		}
	}

	[Theory]
	[InlineData(new long[] { 30_000_000, 40_000_000 }, new long[] { 50_000_000, 60_000_000 }, new long[] { 70_000_000, 80_000_000 })]
	public async Task CoinJoinWithBlameRoundTestAsync(long[] satAmounts1, long[] satAmounts2, long[] satAmounts3)
	{
		int inputCount = satAmounts1.Length;
		var progress = new ConcurrentQueue<string>();
		var observedRounds = new ConcurrentDictionary<string, byte>();

		// At the end of the test a coinjoin transaction has to be created and broadcasted.
		var broadcastedTxTcs = new TaskCompletionSource<Transaction>(TaskCreationOptions.RunContinuationsAsynchronously);

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		cts.Token.Register(() => broadcastedTxTcs.TrySetCanceled(), useSynchronizationContext: false);

		KeyManager keyManager1 = KeyManager.CreateNew(out var _, password: "", Network.Main);
		KeyManager keyManager2 = KeyManager.CreateNew(out var _, password: "", Network.Main);
		KeyManager keyManager3 = KeyManager.CreateNew(out var _, password: "", Network.Main);
		// This fixture explicitly consolidates both inputs expected by its assertions.
		keyManager1.NonPrivateCoinIsolation = false;
		keyManager2.NonPrivateCoinIsolation = false;
		keyManager3.NonPrivateCoinIsolation = false;

		// Three participants register outputs; the second withholds signatures to force blame.
		var participant1Coins = GenerateSmartCoins(keyManager1, satAmounts1, inputCount);
		var participant2CoinsBad = GenerateSmartCoins(keyManager2, satAmounts2, inputCount);
		var participant3Coins = GenerateSmartCoins(keyManager3, satAmounts3, inputCount);

		var coordinatorApp = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.AddMockRpcClient(
				Enumerable.Concat(participant1Coins, participant2CoinsBad).Concat(participant3Coins).ToArray(),
				rpc =>
				{
					rpc.OnGetRawTransactionAsync = (txid, throwIfNotFound) =>
					{
						var tx = Transaction.Create(Network.Main);
						return Task.FromResult(tx);
					};

					// Make the coordinator believe that the transaction is being
					// broadcasted using the RPC interface. Once we receive this tx
					// (the `SendRawTransactionAsync` was invoked) we stop waiting
					// and finish the waiting tasks to finish the test successfully.
					rpc.OnSendRawTransactionAsync = (tx) =>
					{
						broadcastedTxTcs.SetResult(tx);
						return tx.GetHash();
					};
				})
			.ConfigureServices(services =>

				// Instruct the coordinator DI container to use this scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddSingleton(s => new WabiSabiConfig
				{
					AllowP2trInputs = true,
					AllowP2trOutputs = true,
					MaxInputCountByRound = 3 * inputCount,
					// Arena creates another registrable round below one minute. A ten-second
					// fixture splits concurrently starting clients across different rounds.
					StandardInputRegistrationTimeout = TimeSpan.FromSeconds(90),
					BlameInputRegistrationTimeout = TimeSpan.FromMinutes(1),
					// Thirty seconds expires registered Alices on a contended CPU.
					// These three clients share a process/CPU with their coordinator;
					// use the production fail-fast output budget for their ZK work.
					ConnectionConfirmationTimeout = TimeSpan.FromMinutes(1),
					OutputRegistrationTimeout = TimeSpan.FromMinutes(3),
					TransactionSigningTimeout = TimeSpan.FromMinutes(1),
					MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
				})));

		await Task.Delay(100);

		// Create the coinjoin client
		using var honestHttpClient1 = coordinatorApp.CreateDefaultClient(new Uri("http://localhost"), new BlameRequestTraceHandler("honest-1", progress));
		var apiClient1 = _apiApplicationFactory.CreateWabiSabiHttpApiClient(honestHttpClient1);
		using var roundStateUpdater = RoundStateUpdaterForTesting.Create(apiClient1, cts.Token);
		var roundStateProvider = new RoundStateProvider(roundStateUpdater);

		var roundState = await roundStateProvider.CreateRoundAwaiterAsync(roundState => roundState.Phase == Phase.InputRegistration, cts.Token);
		Assert.Equal(TimeSpan.FromMinutes(3), roundState.CoinjoinState.Parameters.OutputRegistrationTimeout);

		using var httpClient = coordinatorApp.CreateDefaultClient(new Uri("http://localhost"), new BlameRequestTraceHandler("withheld-signatures", progress));

		// Creates a mocked HttpClient that says everything is okay when a signature is sent but it doesn't really send it.
		using var nonSigningHttpClientMock = new MockHttpClient();
		nonSigningHttpClientMock.BaseAddress = httpClient.BaseAddress;
		nonSigningHttpClientMock.OnSendAsync = req =>
		{
			Assert.NotNull(req.RequestUri);

			if (req.RequestUri.ToString().Contains("transaction-signature"))
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			}

			return httpClient.SendAsync(req, CancellationToken.None);
		};

		var apiClient2Bad = _apiApplicationFactory.CreateWabiSabiHttpApiClient(nonSigningHttpClientMock);
		using var honestHttpClient3 = coordinatorApp.CreateDefaultClient(new Uri("http://localhost"), new BlameRequestTraceHandler("honest-3", progress));
		var apiClient3 = _apiApplicationFactory.CreateWabiSabiHttpApiClient(honestHttpClient3);

		var coinJoinClient1 = WabiSabiFactory.CreateTestCoinJoinClient(_ => apiClient1, keyManager1, roundStateProvider);
		var coinJoinClient2Bad = WabiSabiFactory.CreateTestCoinJoinClient(_ => apiClient2Bad, keyManager2, roundStateProvider);
		// This seed produces the real output values that previously deadlocked
		// zero-credential routing. Exercise that graph with cryptographic issuance
		// and both independently keyed honest participants through the blame round.
		var coinJoinClient3 = WabiSabiFactory.CreateTestCoinJoinClient(_ => apiClient3, new KeyChain(keyManager3, ""),
			new OutputProvider(new InternalDestinationProvider(keyManager3), RandomExtensions.CreateSeeded(23)),
			roundStateProvider, keyManager3.NonPrivateCoinIsolation);
		void RecordProgress(string participant, CoinJoinProgressEventArgs change)
		{
			if (change is RoundStateChanged changed) { observedRounds.TryAdd(changed.RoundState.Id.ToString(), 0); }
			var detail = change switch
			{
				RoundEnded ended => $"{ended.LastRoundState.Id} ended {ended.LastRoundState.EndRoundState}",
				RoundStateChanged phase => $"{phase.RoundState.Id} {change.GetType().Name}; deadline {phase.TimeoutAt:O}; remaining {(phase.TimeoutAt - DateTimeOffset.UtcNow).TotalSeconds:F2}s",
				_ => change.GetType().Name
			};
			progress.Enqueue($"{DateTimeOffset.UtcNow:O} {participant}: {detail}");
		}
		coinJoinClient1.CoinJoinClientProgress += (_, change) => RecordProgress("honest-1", change);
		coinJoinClient2Bad.CoinJoinClientProgress += (_, change) => RecordProgress("withheld-signatures", change);
		coinJoinClient3.CoinJoinClientProgress += (_, change) => RecordProgress("honest-3", change);

		var participant1CoinjoinTask = coinJoinClient1.StartCoinJoinAsync(() => participant1Coins, cts.Token);
		var participant2CoinjoinTaskBad = coinJoinClient2Bad.StartRoundAsync(participant2CoinsBad, UnrestrictedRound.Instance, roundState, cts.Token);
		var participant3CoinjoinTask = coinJoinClient3.StartCoinJoinAsync(() => participant3Coins, cts.Token);

		try
		{
			await Task.WhenAll(new Task[] { participant2CoinjoinTaskBad, participant1CoinjoinTask, participant3CoinjoinTask });
		}
		finally
		{
			foreach (var entry in progress) { _output.WriteLine(entry); }
			// Only synthetic rounds from this fixture. Preserve the engine's failure
			// reason as well as phase/request timings when CI cannot complete blame.
			foreach (var line in File.ReadLines(Logger.FilePath))
			{
				if (observedRounds.Keys.Any(id => line.Contains("Round " + id[..8], StringComparison.Ordinal)
					|| line.Contains("Round (" + id, StringComparison.Ordinal))) { _output.WriteLine(line); }
			}
		}

		var participant1Result = await participant1CoinjoinTask;
		var participant2ResultBad = await participant2CoinjoinTaskBad;
		var participant3Result = await participant3CoinjoinTask;

		Assert.IsType<SuccessfulCoinJoinResult>(participant1Result);

		// The mock acknowledges witnesses but the coordinator never receives them.
		Assert.IsType<DisruptedCoinJoinResult>(participant2ResultBad);

		Assert.IsType<SuccessfulCoinJoinResult>(participant3Result);

		var broadcastedTx = await broadcastedTxTcs.Task; // wait for the transaction to be broadcasted.
		Assert.NotNull(broadcastedTx);

		// Only the honest participants' coins remain; the withheld signatures caused blame.
		var expectedInputs = participant1Coins.Concat(participant3Coins)
			.Select(x => x.Coin.Outpoint.ToString())
			.Order()
			.ToList();

		var actualInputs = broadcastedTx.Inputs
			.Select(x => x.PrevOut.ToString())
			.Order();

		Assert.Equal(expectedInputs, actualInputs);
	}

	private sealed class BlameRequestTraceHandler(string participant, ConcurrentQueue<string> progress) : DelegatingHandler
	{
		private int _sequence;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var route = request.RequestUri!.AbsolutePath;
			if (route.EndsWith("/status", StringComparison.Ordinal)) { return await base.SendAsync(request, cancellationToken); }
			var number = Interlocked.Increment(ref _sequence);
			var started = Stopwatch.GetTimestamp();
			progress.Enqueue($"{DateTimeOffset.UtcNow:O} {participant}: request {number} {route} started");
			try
			{
				var response = await base.SendAsync(request, cancellationToken);
				progress.Enqueue($"{DateTimeOffset.UtcNow:O} {participant}: request {number} {route} HTTP {(int)response.StatusCode}; {Stopwatch.GetElapsedTime(started).TotalSeconds:F2}s");
				return response;
			}
			catch (Exception error)
			{
				progress.Enqueue($"{DateTimeOffset.UtcNow:O} {participant}: request {number} {route} {error.GetType().Name}; {Stopwatch.GetElapsedTime(started).TotalSeconds:F2}s");
				throw;
			}
		}
	}

	[Theory]
	[InlineData(123456, 0.00, 0.00)]
	public async Task MultiClientsCoinJoinTestAsync(
		int seed,
		double faultInjectorMonkeyAggressiveness,
		double delayInjectorMonkeyAggressiveness)
	{
		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

		const int NumberOfParticipants = 10;
		const int NumberOfCoinsPerParticipant = 2;
		const int ExpectedInputNumber = (NumberOfParticipants * NumberOfCoinsPerParticipant) / 2;

		var coinJoinBroadcasted = new TaskCompletionSource<Transaction>();
		var rpc = BitcoinFactory.GetMockMinimalRpc();
		var onSendRawTransaction = rpc.OnSendRawTransactionAsync;
		rpc.OnSendRawTransactionAsync = tx =>
		{
			onSendRawTransaction?.Invoke(tx);
			if (tx.Inputs.Count > 1)
			{
				coinJoinBroadcasted.SetResult(tx);
			}

			return tx.GetHash();
		};
		var coordinatorApp = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				// Instruct the coordinator DI container to use these two scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddSingleton<IRPCClient>(s => rpc);
				services.AddSingleton(s => new WabiSabiConfig(Path.GetTempFileName())
				{
					MaxRegistrableAmount = Money.Coins(500m),
					MaxInputCountByRound = (int)(ExpectedInputNumber / (1 + (10 * (faultInjectorMonkeyAggressiveness + delayInjectorMonkeyAggressiveness)))),
					StandardInputRegistrationTimeout = TimeSpan.FromSeconds(5 * ExpectedInputNumber),
					BlameInputRegistrationTimeout = TimeSpan.FromSeconds(2 * ExpectedInputNumber),
					ConnectionConfirmationTimeout = TimeSpan.FromSeconds(2 * ExpectedInputNumber),
					OutputRegistrationTimeout = TimeSpan.FromSeconds(5 * ExpectedInputNumber),
					TransactionSigningTimeout = TimeSpan.FromSeconds(3 * ExpectedInputNumber),
					MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
				});
			}));

		var httpClient = coordinatorApp.CreateClient();

		await Task.Delay(100);
		using var httpClientWrapper = new MonkeyHttpClient(
			httpClient,
			() => // This monkey injects `HttpRequestException` randomly to simulate errors in the communication.
			{
				if (Random.Shared.NextDouble() < faultInjectorMonkeyAggressiveness)
				{
					throw new HttpRequestException("Crazy monkey hates you, donkey.");
				}
				return Task.CompletedTask;
			},
			async () => // This monkey injects `Delays` randomly to simulate slow response times.
			{
				await Task.Delay(TimeSpan.FromSeconds(5 * delayInjectorMonkeyAggressiveness), cts.Token).ConfigureAwait(false);
			});
		httpClientWrapper.BaseAddress = httpClient.BaseAddress;

		var apiClient = new WabiSabiHttpApiClient("", new MockHttpClientFactory {OnCreateClient = _ => httpClientWrapper});

		var participants = Enumerable
			.Range(0, NumberOfParticipants)
			.Select(i => new Participant($"participant{i}", rpc, _ => apiClient))
			.ToArray();

		foreach (var participant in participants)
		{
			await participant.GenerateSourceCoinAsync(cts.Token);
		}
		var dummyWallet = new TestWallet("dummy", rpc);
		await dummyWallet.GenerateAsync(101, cts.Token);
		foreach (var participant in participants)
		{
			await participant.GenerateCoinsAsync(NumberOfCoinsPerParticipant, seed, cts.Token);
		}
		await dummyWallet.GenerateAsync(101, cts.Token);

		var tasks = participants.Select(x => x.StartParticipatingAsync(cts.Token)).ToArray();

		var coinjoinTransactionCompletionTask = coinJoinBroadcasted.Task.WaitAsync(cts.Token);
		var participantsFinishedTask = Task.WhenAll(tasks);
		var finishedTask = await Task.WhenAny(participantsFinishedTask, coinjoinTransactionCompletionTask);

		if (finishedTask == coinjoinTransactionCompletionTask)
		{
			var broadcastedCoinjoinTransaction = await coinjoinTransactionCompletionTask;
			var mempool = await rpc.GetRawMempoolAsync();
			var coinjoinFromMempool = await rpc.GetRawTransactionAsync(mempool.Single(), cancellationToken: cts.Token);

			Assert.Equal(broadcastedCoinjoinTransaction.GetHash(), coinjoinFromMempool.GetHash());
		}
		else if (finishedTask == participantsFinishedTask)
		{
			var participantsFinishedSuccessfully = tasks
				.Where(t => t.IsCompletedSuccessfully)
				.Select(t => t.Result)
				.ToArray();

			// In case some participants claim to have finished successfully then wait a second for seeing
			// the coinjoin in the mempool. This seems really hard to believe but just in case.
			if (participantsFinishedSuccessfully.All(x => x is SuccessfulCoinJoinResult))
			{
				await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
				var mempool = await rpc.GetRawMempoolAsync(cts.Token);
				Assert.Single(mempool);
			}
			else if (participantsFinishedSuccessfully.All(x => x is FailedCoinJoinResult))
			{
				throw new Exception("All participants finished, but CoinJoin still not in the mempool (no more blame rounds).");
			}
			else if (participantsFinishedSuccessfully.Length == 0 && !cts.IsCancellationRequested)
			{
				var exceptions = tasks
					.Where(x => x.IsFaulted)
					.Select(x => new Exception("Something went wrong", x.Exception))
					.ToArray();
				throw new AggregateException(exceptions);
			}
			else
			{
				throw new Exception("All participants finished, but CoinJoin still not in the mempool.");
			}
		}
		else
		{
			throw new Exception("This is not so possible.");
		}
	}

	[Fact]
	public async Task RegisterCoinIdempotencyAsync()
	{
		using var signingKey = new Key();
		Coin coinToRegister = new(
			fromOutpoint: BitcoinFactory.CreateOutPoint(),
			fromTxOut: new TxOut(Money.Coins(1), signingKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)));

		using var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				var rpc = BitcoinFactory.GetMockMinimalRpc();
				rpc.OnGetTxOutAsync = (_, _, _) => new()
				{
					Confirmations = 101,
					IsCoinBase = false,
					ScriptPubKeyType = "witness_v0_keyhash",
					TxOut = coinToRegister.TxOut
				};
				rpc.OnGetRawTransactionAsync = (txid, throwIfNotFound) =>
				{
					var tx = Transaction.Create(Network.Main);
					return Task.FromResult(tx);
				};
				services.AddSingleton<IRPCClient>(s => rpc);
			})).CreateClient();

		var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
		var rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, CancellationToken.None)).RoundStates;
		var round = rounds.First(x => x.CoinjoinState is ConstructionState);
		using var stutteredHttpClient = new StuttererHttpClient(httpClient);
		var stutteredApiClient = new ArenaClient(
			apiClient.AmountCredentialClient,
			apiClient.VsizeCredentialClient,
			apiClient.CoordinatorIdentifier,
			_apiApplicationFactory.CreateWabiSabiHttpApiClient(stutteredHttpClient));

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);
		var response = await stutteredApiClient.RegisterInputAsync(round.Id, coinToRegister.Outpoint, ownershipProof, CancellationToken.None);

		Assert.NotEqual(Guid.Empty, response.Value);
	}

	private SmartCoin[] GenerateSmartCoins(KeyManager keyManager, long[] amounts, int inputCount)
	{
		var anonscore = 0;

		return keyManager.GetKeys()
			.Take(inputCount)
			.Select((x, i) =>
			{
				anonscore++;
				return BitcoinFactory.CreateSmartCoin(x, Money.Satoshis(amounts[i]), true, anonscore);
			})
			.ToArray();
	}

	public class TestableRpcClient : RpcClientBase
	{
		public TestableRpcClient(RpcClientBase rpc)
			: base(rpc.RpcClient)
		{
		}

		public Action<Transaction>? AfterSendRawTransaction { get; set; }

		public override async Task<uint256> SendRawTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
		{
			var ret = await base.SendRawTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
			AfterSendRawTransaction?.Invoke(transaction);
			return ret;
		}
	}
}
