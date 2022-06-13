using System.Collections.Immutable;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NBitcoin;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using Xunit.Abstractions;

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

		var bannedOutPoint = BitcoinFactory.CreateOutPoint();

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				var inmate = new Inmate(bannedOutPoint, Punishment.LongBanned, DateTimeOffset.UtcNow, uint256.One);
				services.AddScoped<Prison>(_ => new Prison(new[] {inmate}));
			})).CreateClient();

		var apiClient = await _apiApplicationFactory.CreateArenaClientAsync(httpClient);
		var rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, timeoutCts.Token)).RoundStates;
		var round = rounds.First(x => x.CoinjoinState is ConstructionState);

		// If an output is not in the utxo dataset then it is not unspent, this
		// means that the output is spent or simply doesn't even exist.
		using var signingKey = new Key();
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () =>
			await apiClient.RegisterInputAsync(round.Id, bannedOutPoint, ownershipProof, timeoutCts.Token));

		Assert.Equal(WabiSabiProtocolErrorCode.InputLongBanned, ex.ErrorCode);
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
		var keyManager = KeyManager.CreateNew(out var _, password: "", Network.Main);
		keyManager.AssertCleanKeysIndexed();
		var coins = keyManager.GetKeys()
			.Take(inputCount)
			.Select((x, i) => BitcoinFactory.CreateSmartCoin(x, amounts[i]))
			.ToArray();
		_output.WriteLine("Coins were created successfully");

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder.AddMockRpcClient(coins, rpc =>
			{
				// Make the coordinator believe that the transaction is being
				// broadcasted using the RPC interface. Once we receive this tx
				// (the `SendRawTransationAsync` was invoked) we stop waiting
				// and finish the waiting tasks to finish the test successfully.
				rpc.OnSendRawTransactionAsync = (tx) =>
				{
					transactionCompleted.SetResult(tx);
					return tx.GetHash();
				};
			})
			.ConfigureServices(services =>
			{
				// Instruct the coordinator DI container to use these two scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddScoped(s => new WabiSabiConfig
				{
					MaxInputCountByRound = inputCount,
					StandardInputRegistrationTimeout = TimeSpan.FromSeconds(60),
					ConnectionConfirmationTimeout = TimeSpan.FromSeconds(60),
					OutputRegistrationTimeout = TimeSpan.FromSeconds(60),
					TransactionSigningTimeout = TimeSpan.FromSeconds(60),
					MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
				}

				);
				// Emulate that the first coin is coming from a coinjoin.
				services.AddScoped(s => new InMemoryCoinJoinIdStore(new[] { coins[0].Coin.Outpoint.Hash }));
			})).CreateClient();

		// Create the coinjoin client
		using PersonCircuit personCircuit = new();
		IHttpClient httpClientWrapper = new HttpClientWrapper(httpClient);
		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
		var mockHttpClientFactory = new Mock<IWasabiHttpClientFactory>(MockBehavior.Strict);

		mockHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithPersonCircuit(out httpClientWrapper))
			.Returns(personCircuit);

		mockHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithCircuitPerRequest())
			.Returns(httpClientWrapper);

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));
		cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

		using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), apiClient);

		await roundStateUpdater.StartAsync(CancellationToken.None);

		var coinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(mockHttpClientFactory.Object, keyManager, roundStateUpdater);

		// Run the coinjoin client task.
		Assert.True((await coinJoinClient.StartCoinJoinAsync(coins, cts.Token)).SuccessfulBroadcast);

		var broadcastedTx = await transactionCompleted.Task; // wait for the transaction to be broadcasted.
		Assert.NotNull(broadcastedTx);

		await roundStateUpdater.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task FailToRegisterOutputsCoinJoinTestAsync()
	{
		var amounts = new long[] {10_000_000, 20_000_000, 30_000_000};
		int inputCount = amounts.Length;

		// At the end of the test a coinjoin transaction has to be created and broadcasted.
		var transactionCompleted = new TaskCompletionSource<Transaction>();

		// Create a key manager and use it to create fake coins.
		_output.WriteLine("Creating key manager...");
		var keyManager = KeyManager.CreateNew(out var _, password: "", Network.Main);
		keyManager.AssertCleanKeysIndexed();
		var coins = keyManager.GetKeys()
			.Take(inputCount)
			.Select((x, i) => BitcoinFactory.CreateSmartCoin(x, amounts[i]))
			.ToArray();
		_output.WriteLine("Coins were created successfully");

		keyManager.AssertLockedInternalKeysIndexed(14);
		var outputScriptCandidates = keyManager
			.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked)
			.Select(x => x.PubKey.WitHash.ScriptPubKey)
			.ToImmutableArray();

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
			builder
			.AddMockRpcClient(coins, rpc => { })
			.ConfigureServices(services =>
			{
				// Instruct the coordinator DI container to use this scoped
				// services to build everything (WabiSabi controller, arena, etc)
				services.AddScoped(s => new WabiSabiConfig
				{
					MaxInputCountByRound = inputCount,
					StandardInputRegistrationTimeout = TimeSpan.FromSeconds(60),
					ConnectionConfirmationTimeout = TimeSpan.FromSeconds(60),
					OutputRegistrationTimeout = TimeSpan.FromSeconds(60),
					TransactionSigningTimeout = TimeSpan.FromSeconds(60),
					MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
				});

				// Emulate that the all our outputs had been already used in the past.
				// the server will prevent the registration and fail with an WabiSabiProtocolError.
				services.AddScoped(s => new CoinJoinScriptStore(outputScriptCandidates));
			})).CreateClient();

		// Create the coinjoin client
		using PersonCircuit personCircuit = new();
		IHttpClient httpClientWrapper = new HttpClientWrapper(httpClient);
		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
		var mockHttpClientFactory = new Mock<IWasabiHttpClientFactory>(MockBehavior.Strict);

		mockHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithPersonCircuit(out httpClientWrapper))
			.Returns(personCircuit);

		mockHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithCircuitPerRequest())
			.Returns(httpClientWrapper);

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));
		cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

		using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), apiClient);

		await roundStateUpdater.StartAsync(CancellationToken.None);

		var coinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(mockHttpClientFactory.Object, keyManager, roundStateUpdater);

		bool failedBecauseNotAllAlicesSigned = false;
		void HandleCoinJoinProgress(object? sender, CoinJoinProgressEventArgs coinJoinProgress)
		{
			if (coinJoinProgress is RoundEnded roundEnded)
			{
				if (roundEnded.LastRoundState.EndRoundState is EndRoundState.NotAllAlicesSign)
				{
					failedBecauseNotAllAlicesSigned = true;
				}
				cts.Cancel(); // this is what we were waiting for so, end the test.
			}
		}

		try
		{
			coinJoinClient.CoinJoinClientProgress += HandleCoinJoinProgress;

			// Run the coinjoin client task.
			await coinJoinClient.StartCoinJoinAsync(coins, cts.Token);
			throw new Exception("Coinjoin should have never finished successfully.");
		}
		catch (OperationCanceledException)
		{
			Assert.True(failedBecauseNotAllAlicesSigned);
		}
		finally
		{
			coinJoinClient.CoinJoinClientProgress -= HandleCoinJoinProgress;
			await roundStateUpdater.StopAsync(CancellationToken.None);
		}
	}

	[Theory]
	[InlineData(new long[] { 20_000_000, 40_000_000, 60_000_000, 80_000_000 })]
	public async Task CoinJoinWithBlameRoundTestAsync(long[] amounts)
	{
		int inputCount = amounts.Length;

		// At the end of the test a coinjoin transaction has to be created and broadcasted.
		var transactionCompleted = new TaskCompletionSource<Transaction>(TaskCreationOptions.RunContinuationsAsynchronously);

		// Total test timeout.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		cts.Token.Register(() => transactionCompleted.TrySetCanceled(), useSynchronizationContext: false);

		var keyManager1 = KeyManager.CreateNew(out var _, password: "", Network.Main);
		keyManager1.AssertCleanKeysIndexed();

		var keyManager2 = KeyManager.CreateNew(out var _, password: "", Network.Main);
		keyManager2.AssertCleanKeysIndexed();

		var coins = keyManager1.GetKeys()
			.Take(inputCount)
			.Select((x, i) => BitcoinFactory.CreateSmartCoin(x, amounts[i]))
			.ToArray();

		var badCoins = keyManager2.GetKeys()
			.Take(inputCount)
			.Select((x, i) => BitcoinFactory.CreateSmartCoin(x, amounts[i]))
			.ToArray();

		var httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
		builder.AddMockRpcClient(Enumerable.Concat(coins, badCoins).ToArray(), rpc =>
		{
			rpc.OnGetRawTransactionAsync = (txid, throwIfNotFound) =>
			{
				var tx = Transaction.Create(Network.Main);
				return Task.FromResult(tx);
			};

			// Make the coordinator believe that the transaction is being
			// broadcasted using the RPC interface. Once we receive this tx
			// (the `SendRawTransationAsync` was invoked) we stop waiting
			// and finish the waiting tasks to finish the test successfully.
			rpc.OnSendRawTransactionAsync = (tx) =>
			{
				transactionCompleted.SetResult(tx);
				return tx.GetHash();
			};
		})
		.ConfigureServices(services =>
		{
			// Instruct the coordinator DI container to use this scoped
			// services to build everything (WabiSabi controller, arena, etc)
			services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig
			{
				MaxInputCountByRound = 2 * inputCount,
				StandardInputRegistrationTimeout = TimeSpan.FromSeconds(60),
				BlameInputRegistrationTimeout = TimeSpan.FromSeconds(60),
				ConnectionConfirmationTimeout = TimeSpan.FromSeconds(60),
				OutputRegistrationTimeout = TimeSpan.FromSeconds(60),
				TransactionSigningTimeout = TimeSpan.FromSeconds(5 * inputCount),
				MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
			});
		})).CreateClient();

		// Create the coinjoin client
		using PersonCircuit personCircuit = new();
		IHttpClient httpClientWrapper = new HttpClientWrapper(httpClient);

		var apiClient = _apiApplicationFactory.CreateWabiSabiHttpApiClient(httpClient);
		var mockHttpClientFactory = new Mock<IWasabiHttpClientFactory>(MockBehavior.Strict);
		mockHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithPersonCircuit(out httpClientWrapper))
			.Returns(personCircuit);

		mockHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithCircuitPerRequest())
			.Returns(httpClientWrapper);

		using var roundStateUpdater = new RoundStateUpdater(TimeSpan.FromSeconds(1), apiClient);
		await roundStateUpdater.StartAsync(CancellationToken.None);

		var roundState = await roundStateUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cts.Token);

		var coinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(mockHttpClientFactory.Object, keyManager1, roundStateUpdater);

		// Run the coinjoin client task.
		var coinJoinTask = Task.Run(async () => await coinJoinClient.StartCoinJoinAsync(coins, cts.Token).ConfigureAwait(false), cts.Token);

		// Creates a IBackendHttpClientFactory that creates an HttpClient that says everything is okay
		// when a signature is sent but it doesn't really send it.
		var nonSigningHttpClientMock = new Mock<HttpClientWrapper>(MockBehavior.Strict, httpClient);
		nonSigningHttpClientMock
			.Setup(httpClient => httpClient.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.CallBase();
		nonSigningHttpClientMock
			.Setup(httpClient => httpClient.SendAsync(It.Is<HttpRequestMessage>(
				req => req.RequestUri!.AbsolutePath.Contains("transaction-signature")),
				It.IsAny<CancellationToken>()))
			.ThrowsAsync(new HttpRequestException("Something was wrong posting the signature."));

		IHttpClient nonSigningHttpClient = nonSigningHttpClientMock.Object;

		var mockNonSigningHttpClientFactory = new Mock<IWasabiHttpClientFactory>(MockBehavior.Strict);
		mockNonSigningHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithPersonCircuit(out nonSigningHttpClient))
			.Returns(personCircuit);

		mockNonSigningHttpClientFactory
			.Setup(factory => factory.NewHttpClientWithCircuitPerRequest())
			.Returns(nonSigningHttpClient);

		var badCoinJoinClient = WabiSabiFactory.CreateTestCoinJoinClient(mockNonSigningHttpClientFactory.Object, keyManager2, roundStateUpdater);

		var badCoinsTask = Task.Run(async () => await badCoinJoinClient.StartRoundAsync(badCoins, roundState, cts.Token).ConfigureAwait(false), cts.Token);

		// BadCoinsTask will throw.
		await Assert.ThrowsAsync<AggregateException>(async () => await Task.WhenAll(new Task[] { badCoinsTask, coinJoinTask }));

		Assert.True(badCoinsTask.IsFaulted);
		Assert.True(coinJoinTask.Result.SuccessfulBroadcast);

		var broadcastedTx = await transactionCompleted.Task; // wait for the transaction to be broadcasted.
		Assert.NotNull(broadcastedTx);

		Assert.Equal(
			coins.Select(x => x.Coin.Outpoint.ToString()).OrderBy(x => x),
			broadcastedTx.Inputs.Select(x => x.PrevOut.ToString()).OrderBy(x => x));

		await roundStateUpdater.StopAsync(CancellationToken.None);
	}

	[Theory]
	[InlineData(123456)]
	public async Task MultiClientsCoinJoinTestAsync(int seed)
	{
		const int NumberOfParticipants = 10;
		const int NumberOfCoinsPerParticipant = 2;
		const int ExpectedInputNumber = (NumberOfParticipants * NumberOfCoinsPerParticipant) / 2;

		var node = await TestNodeBuilder.CreateForHeavyConcurrencyAsync();
		try
		{
			var rpc = node.RpcClient;

			var app = _apiApplicationFactory.WithWebHostBuilder(builder =>
				builder.ConfigureServices(services =>
				{
					// Instruct the coordinator DI container to use these two scoped
					// services to build everything (WabiSabi controller, arena, etc)
					services.AddScoped<IRPCClient>(s => rpc);
					services.AddScoped<WabiSabiConfig>(s => new WabiSabiConfig(Path.GetTempFileName())
					{
						MaxRegistrableAmount = Money.Coins(500m),
						MaxInputCountByRound = ExpectedInputNumber,
						StandardInputRegistrationTimeout = TimeSpan.FromSeconds(10 * ExpectedInputNumber),
						ConnectionConfirmationTimeout = TimeSpan.FromSeconds(20 * ExpectedInputNumber),
						OutputRegistrationTimeout = TimeSpan.FromSeconds(20 * ExpectedInputNumber),
						TransactionSigningTimeout = TimeSpan.FromSeconds(20 * ExpectedInputNumber),
						MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
					});
				}));

			using PersonCircuit personCircuit = new();
			IHttpClient httpClientWrapper = new HttpClientWrapper(app.CreateClient());

			var mockHttpClientFactory = new Mock<IWasabiHttpClientFactory>(MockBehavior.Strict);
			mockHttpClientFactory
				.Setup(factory => factory.NewHttpClientWithPersonCircuit(out httpClientWrapper))
				.Returns(personCircuit);

			mockHttpClientFactory
				.Setup(factory => factory.NewHttpClientWithCircuitPerRequest())
				.Returns(httpClientWrapper);

			mockHttpClientFactory
				.Setup(factory => factory.NewHttpClientWithDefaultCircuit())
				.Returns(httpClientWrapper);

			// Total test timeout.
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40 * ExpectedInputNumber));

			var participants = Enumerable
				.Range(0, NumberOfParticipants)
				.Select(i => new Participant($"participant{i}", rpc, mockHttpClientFactory.Object))
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

			while ((await rpc.GetRawMempoolAsync()).Length == 0)
			{
				if (cts.IsCancellationRequested)
				{
					throw new TimeoutException("Coinjoin was not propagated.");
				}

				await Task.Delay(500, cts.Token);

				if (tasks.FirstOrDefault(t => t.IsFaulted)?.Exception is { } exc)
				{
					throw exc;
				}
			}
			var mempool = await rpc.GetRawMempoolAsync();
			var coinjoin = await rpc.GetRawTransactionAsync(mempool.Single());

			Assert.True(coinjoin.Outputs.Count <= ExpectedInputNumber);
			Assert.Equal(ExpectedInputNumber, coinjoin.Inputs.Count);
		}
		finally
		{
			await node.TryStopAsync();
		}
	}

	[Fact]
	public async Task RegisterCoinIdempotencyAsync()
	{
		using Key signingKey = new();
		Coin coinToRegister = new(
			fromOutpoint: BitcoinFactory.CreateOutPoint(),
			fromTxOut: new TxOut(Money.Coins(1), signingKey.PubKey.WitHash.ScriptPubKey));

		using HttpClient httpClient = _apiApplicationFactory.WithWebHostBuilder(builder =>
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
				services.AddScoped<IRPCClient>(s => rpc);
			})).CreateClient();

		ArenaClient apiClient = await _apiApplicationFactory.CreateArenaClientAsync(new StuttererHttpClient(httpClient));
		RoundState[] rounds = (await apiClient.GetStatusAsync(RoundStateRequest.Empty, CancellationToken.None)).RoundStates;
		RoundState round = rounds.First(x => x.CoinjoinState is ConstructionState);

		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(signingKey, round.Id);
		var (response, _) = await apiClient.RegisterInputAsync(round.Id, coinToRegister.Outpoint, ownershipProof, CancellationToken.None);

		Assert.NotEqual(Guid.Empty, response.Value);
	}
}

public class StuttererHttpClient : HttpClientWrapper
{
	public StuttererHttpClient(HttpClient httpClient) : base(httpClient)
	{
	}

	public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
	{
		using HttpRequestMessage requestClone1 = request.Clone();
		using HttpRequestMessage requestClone2 = request.Clone();

		HttpResponseMessage result1 = await base.SendAsync(requestClone1, token).ConfigureAwait(false);
		HttpResponseMessage result2 = await base.SendAsync(requestClone2, token).ConfigureAwait(false);

		string content1 = await result1.Content.ReadAsStringAsync(token).ConfigureAwait(false);
		string content2 = await result2.Content.ReadAsStringAsync(token).ConfigureAwait(false);

		Assert.Equal(content1, content2);
		return result2;
	}
}
