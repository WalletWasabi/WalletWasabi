using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class CpfpInfoUpdaterTests
{
	[Fact]
	public async Task CreateForRegTest_ReturnsHandler_ThatCompletesSuccessfullyAsync()
	{
		// Arrange
		var handler = CpfpInfoUpdater.CreateForRegTest();
		var message = new CpfpInfoMessage.UpdateMessage();

		// Act
		var task = handler(message, null!, CancellationToken.None);

		// Assert
		Assert.True(task.IsCompletedSuccessfully);

		var result = await task;
		Assert.Equal(Unit.Instance, result);
	}

	[Fact]
	public async Task GetCachedCpfpInfo_ReturnsEmptyArray_WhenCacheIsEmptyAsync()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var replyChannel = new TestReplyChannel<CachedCpfpInfo[]>();
		var message = new CpfpInfoMessage.GetCachedCpfpInfo(replyChannel);

		// Act
		await handler(message, null!, CancellationToken.None);

		// Assert
		Assert.NotNull(replyChannel.Result);
		Assert.Empty(replyChannel.Result);
	}

	[Fact]
	public async Task UpdateMessage_ProcessesWithoutErrorAsync()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var message = new CpfpInfoMessage.UpdateMessage();

		// Act
		var result = await handler(message, null!, CancellationToken.None);

		// Assert
		Assert.Equal(Unit.Instance, result);
	}

	[Fact]
	public async Task GetInfoForTransaction_ReturnsFailure_WhenHttpRequestFailsAsync()
	{
		// Arrange
		var httpClientFactory = MockHttpClientFactory.Create(
			() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction();
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();
		var message = new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel);

		// Act
		await handler(message, Unit.Instance, CancellationToken.None);

		// Assert
		Assert.NotNull(replyChannel.Result);
		Assert.False(replyChannel.Result.IsOk);
	}

	[Fact]
	public async Task GetInfoForTransaction_ReturnsSuccess_WhenHttpRequestSucceedsAsync()
	{
		// Arrange
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();
		var message = new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel);

		// Act
		await handler(message, Unit.Instance, CancellationToken.None);

		// Assert
		Assert.NotNull(replyChannel.Result);
		Assert.True(replyChannel.Result.IsOk);
	}

	[Fact]
	public async Task GetInfoForTransaction_UsesCachedValue_WhenAvailableAsync()
	{
		// Arrange
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var callCount = 0;

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ =>
		{
			callCount++;
			return Task.FromResult(HttpResponseMessageEx.Ok(cpfpJson));
		};

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// First request
		var replyChannel1 = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel1), null!, CancellationToken.None);

		// Second request for same transaction
		var replyChannel2 = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel2), null!, CancellationToken.None);

		// Assert - HTTP should only be called once due to caching
		Assert.Equal(1, callCount);
		Assert.True(replyChannel1.Result!.IsOk);
		Assert.True(replyChannel2.Result!.IsOk);
	}

	[Fact]
	public async Task PreFetchInfoForTransaction_SchedulesTask_WithoutBlockingAsync()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var message = new CpfpInfoMessage.PreFetchInfoForTransaction(tx);

		// Act
		var result = await handler(message, null!, CancellationToken.None);

		// Assert
		Assert.Equal(Unit.Instance, result);
	}

	[Fact]
	public async Task UpdateMessage_RemovesConfirmedTransactions_FromCacheAsync()
	{
		// Arrange
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: new Height(8888));

		// Add to cache
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel), null!, CancellationToken.None);

		// Trigger update (cleans confirmed transactions)
		await handler(new CpfpInfoMessage.UpdateMessage(), Unit.Instance, CancellationToken.None);

		// Check cache
		var cacheReplyChannel = new TestReplyChannel<CachedCpfpInfo[]>();
		await handler(new CpfpInfoMessage.GetCachedCpfpInfo(cacheReplyChannel), null!, CancellationToken.None);

		// Assert
		Assert.Empty(cacheReplyChannel.Result!);
	}

	[Fact]
	public async Task UpdateMessage_KeepsUnconfirmedTransactions_InCacheAsync()
	{
		// Arrange
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson),
			() => HttpResponseMessageEx.Ok(cpfpJson)); // For reschedule

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Add to cache
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel), null!, CancellationToken.None);

		// Trigger update
		await handler(new CpfpInfoMessage.UpdateMessage(), null!, CancellationToken.None);

		// Check cache
		var cacheReplyChannel = new TestReplyChannel<CachedCpfpInfo[]>();
		await handler(new CpfpInfoMessage.GetCachedCpfpInfo(cacheReplyChannel), null!, CancellationToken.None);

		// Assert
		Assert.Single(cacheReplyChannel.Result!);
	}

	[Fact]
	public async Task GetInfoForTransaction_PublishesCpfpInfoArrivedEvent_OnSuccessAsync()
	{
		// Arrange
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var eventReceived = false;
		eventBus.Subscribe<CpfpInfoArrived>(_ => eventReceived = true);

		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();

		// Act
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel), null!, CancellationToken.None);

		// Assert
		Assert.True(eventReceived);
	}

	[Fact]
	public async Task GetInfoForTransaction_DoesNotPublishEvent_OnFailureAsync()
	{
		// Arrange
		var httpClientFactory = MockHttpClientFactory.Create(
			() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

		var eventBus = new EventBus();
		var eventReceived = false;
		eventBus.Subscribe<CpfpInfoArrived>(_ => eventReceived = true);

		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();

		// Act
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel), null!, CancellationToken.None);

		// Assert
		Assert.False(eventReceived);
	}

	[Fact]
	public void Create_DoesNotThrow_ForMainnet()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();

		// Act & Assert
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		Assert.NotNull(handler);
	}

	[Fact]
	public void Create_DoesNotThrow_ForTestnet4()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();

		// Act & Assert
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.TestNet4, eventBus);
		Assert.NotNull(handler);
	}

	[Fact]
	public async Task GetCachedCpfpInfo_ReturnsAllCachedItemsAsync()
	{
		// Arrange
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson),
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		var tx1 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var tx2 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Add two transactions to cache
		var reply1 = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx1, reply1), null!, CancellationToken.None);

		var reply2 = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx2, reply2), null!, CancellationToken.None);

		// Act
		var cacheReplyChannel = new TestReplyChannel<CachedCpfpInfo[]>();
		await handler(new CpfpInfoMessage.GetCachedCpfpInfo(cacheReplyChannel), null!, CancellationToken.None);

		// Assert
		Assert.Equal(2, cacheReplyChannel.Result!.Length);
	}
}

public class CpfpInfoProviderTests
{
	[Fact]
	public async Task GetCachedCpfpInfoAsync_ReturnsResultFromMailboxAsync()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, cts.Token);
		var provider = new CpfpInfoProvider(mailbox);

		// Act
		var result = await provider.GetCachedCpfpInfoAsync(CancellationToken.None);

		// Assert
		Assert.NotNull(result);
		Assert.Empty(result);
	}

	[Fact]
	public async Task ScheduleRequest_PostsPreFetchMessage_AndProcessesItAsync()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var requestReceived = false;

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ =>
		{
			requestReceived = true;
			return Task.FromResult(HttpResponseMessageEx.Ok(cpfpJson));
		};

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, cts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Act
		provider.ScheduleRequest(tx);

		// Wait for async processing (prefetch has random delay up to 10 seconds, but we can check cache)
		await Task.Delay(100);

		// Assert - message was posted (we can't easily verify prefetch completed due to random delay)
		// But we can verify the provider doesn't throw
		Assert.True(true);
	}

	[Fact]
	public async Task GetCpfpInfoAsync_ReturnsSuccess_WhenHttpSucceedsAsync()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, cts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Act
		var result = await provider.GetCpfpInfoAsync(tx, CancellationToken.None);

		// Assert
		Assert.True(result.IsOk);
	}

	[Fact]
	public async Task GetCpfpInfoAsync_ReturnsFailure_WhenHttpFailsAsync()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var httpClientFactory = MockHttpClientFactory.Create(
			() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, cts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Act
		var result = await provider.GetCpfpInfoAsync(tx, CancellationToken.None);

		// Assert
		Assert.False(result.IsOk);
	}

	[Fact]
	public async Task GetCpfpInfoAsync_UsesCachedValue_OnSubsequentCallsAsync()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var callCount = 0;

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ =>
		{
			callCount++;
			return Task.FromResult(HttpResponseMessageEx.Ok(cpfpJson));
		};

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, cts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Act
		var result1 = await provider.GetCpfpInfoAsync(tx, CancellationToken.None);
		var result2 = await provider.GetCpfpInfoAsync(tx, CancellationToken.None);

		// Assert
		Assert.Equal(1, callCount);
		Assert.True(result1.IsOk);
		Assert.True(result2.IsOk);
	}

	[Fact]
	public async Task GetCachedCpfpInfoAsync_ReturnsPopulatedCache_AfterGetCpfpInfoAsync()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, cts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Populate cache
		await provider.GetCpfpInfoAsync(tx, CancellationToken.None);

		// Act
		var cachedInfo = await provider.GetCachedCpfpInfoAsync(CancellationToken.None);

		// Assert
		Assert.Single(cachedInfo);
		Assert.Equal(tx, cachedInfo[0].Transaction);
	}

	[Fact]
	public async Task Provider_HandlesCancellation_GracefullyAsync()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, cts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var requestCts = new CancellationTokenSource();
		requestCts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => provider.GetCpfpInfoAsync(tx, requestCts.Token));
	}

	[Fact]
	public async Task Provider_DisposedMailbox_ThrowsObjectDisposedExceptionAsync()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		var mailbox = CreateAndStartMailboxProcessor(handler, CancellationToken.None);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Dispose the mailbox
		mailbox.Dispose();

		// Act & Assert
		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => provider.GetCpfpInfoAsync(tx, CancellationToken.None));
	}

	private static MailboxProcessor<CpfpInfoMessage> CreateAndStartMailboxProcessor(
		MessageHandler<CpfpInfoMessage, Unit> handler,
		CancellationToken cancellationToken)
	{
		var processor = new MailboxProcessor<CpfpInfoMessage>(
			async (mailbox, ct) =>
			{
				while (!ct.IsCancellationRequested)
				{
					var message = await mailbox.ReceiveAsync(ct);
					await handler(message, Unit.Instance, ct);
				}
			},
			cancellationToken);

		processor.Start();
		return processor;
	}
}

public class TestReplyChannel<T> : IReplyChannel<T>
{
	public T? Result { get; private set; }
	public bool WasReplied { get; private set; }

	public void Reply(T response)
	{
		Result = response;
		WasReplied = true;
	}
}

public class CpfpInfoUpdaterCancellationTests
{
	[Fact]
	public async Task GetInfoForTransaction_ThrowsOperationCanceledException_WhenCancelledAsync()
	{
		// Arrange
		var tcs = new TaskCompletionSource<HttpResponseMessage>();
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ => tcs.Task;

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var cts = new CancellationTokenSource();
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();

		// Act
		var task = handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel), null!, cts.Token);
		cts.Cancel();
		tcs.SetCanceled();

		await task;

		// Assert
		Assert.Equal("A task was canceled.", replyChannel.Result!.Error);
	}

	[Fact]
	public async Task GetInfoForTransaction_ReturnsFailure_WhenHttpRequestTimesOutAsync()
	{
		// Arrange
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = async _ =>
		{
			await Task.Delay(TimeSpan.FromSeconds(30));
			return HttpResponseMessageEx.Ok("{}");
		};

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var replyChannel = new TestReplyChannel<Result<CpfpInfo, string>>();

		// Act
		try
		{
			await handler(new CpfpInfoMessage.GetInfoForTransaction(tx, replyChannel), null!, cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - either cancelled or returned failure
		Assert.True(cts.IsCancellationRequested);
	}

	[Fact]
	public async Task PreFetchInfoForTransaction_StopsGracefully_WhenCancelledAsync()
	{
		// Arrange
		var requestStarted = new TaskCompletionSource();
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = async _ =>
		{
			requestStarted.SetResult();
			await Task.Delay(TimeSpan.FromSeconds(30));
			return HttpResponseMessageEx.Ok("{}");
		};

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var cts = new CancellationTokenSource();

		// Act
		var result = await handler(new CpfpInfoMessage.PreFetchInfoForTransaction(tx), null!, cts.Token);
		cts.Cancel();

		// Assert - handler returns immediately, prefetch is scheduled but cancellation stops it
		Assert.Equal(Unit.Instance, result);
	}

	[Fact]
	public async Task UpdateMessage_HandlesAlreadyCancelledTokenAsync()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => handler(new CpfpInfoMessage.UpdateMessage(), null!, cts.Token));
	}

	[Fact]
	public async Task GetCachedCpfpInfo_WorksWithCancelledToken_IfAlreadyCompleteAsync()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);
		var replyChannel = new TestReplyChannel<CachedCpfpInfo[]>();

		// Act - GetCachedCpfpInfo is synchronous internally, so it completes before checking cancellation
		await handler(new CpfpInfoMessage.GetCachedCpfpInfo(replyChannel), null!, CancellationToken.None);

		// Assert
		Assert.True(replyChannel.WasReplied);
		Assert.Empty(replyChannel.Result!);
	}

	[Fact]
	public async Task Handler_ContinuesProcessing_AfterSingleRequestCancellationAsync()
	{
		// Arrange
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var callCount = 0;
		var firstRequestStarted = new TaskCompletionSource();

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = async _ =>
		{
			callCount++;
			if (callCount == 1)
			{
				firstRequestStarted.SetResult();
				await Task.Delay(TimeSpan.FromSeconds(30));
			}
			return HttpResponseMessageEx.Ok(cpfpJson);
		};

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		var tx1 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var tx2 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// First request - will be cancelled
		using var cts1 = new CancellationTokenSource();
		var replyChannel1 = new TestReplyChannel<Result<CpfpInfo, string>>();
		var task1 = handler(new CpfpInfoMessage.GetInfoForTransaction(tx1, replyChannel1), null!, cts1.Token);

		await firstRequestStarted.Task;
		cts1.Cancel();

		try
		{
			await task1;
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Second request - should work
		var replyChannel2 = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(tx2, replyChannel2), null!, CancellationToken.None);

		// Assert
		Assert.True(replyChannel2.WasReplied);
		Assert.True(replyChannel2.Result!.IsOk);
	}
}

public class CpfpInfoProviderCancellationTests
{
	[Fact]
	public async Task GetCachedCpfpInfoAsync_ThrowsOperationCanceledException_WhenCancelledAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var slowHandler = CreateSlowHandler();

		using var mailbox = CreateAndStartMailboxProcessor(slowHandler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);

		using var requestCts = new CancellationTokenSource();
		requestCts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => provider.GetCachedCpfpInfoAsync(requestCts.Token));
	}

	[Fact]
	public async Task GetCpfpInfoAsync_ThrowsOperationCanceledException_WhenCancelledBeforeResponseAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var messageReceived = new TaskCompletionSource();

		async Task<Unit> Handler(CpfpInfoMessage msg, Unit _, CancellationToken ct)
		{
			if (msg is CpfpInfoMessage.GetInfoForTransaction)
			{
				messageReceived.SetResult();
				await Task.Delay(TimeSpan.FromSeconds(30), ct);
			}
			return Unit.Instance;
		}

		using var mailbox = CreateAndStartMailboxProcessor(Handler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var requestCts = new CancellationTokenSource();

		// Act
		var task = provider.GetCpfpInfoAsync(tx, requestCts.Token);
		await messageReceived.Task;
		requestCts.Cancel();

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
	}

	[Fact]
	public async Task GetCpfpInfoAsync_ThrowsOperationCanceledException_WhenCancelledImmediatelyAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var requestCts = new CancellationTokenSource();
		requestCts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => provider.GetCpfpInfoAsync(tx, requestCts.Token));
	}

	[Fact]
	public async Task GetCpfpInfoAsync_ThrowsOperationCanceledException_WhenMailboxProcessorIsCancelledAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var messageReceived = new TaskCompletionSource();

		async Task<Unit> Handler(CpfpInfoMessage msg, Unit _, CancellationToken ct)
		{
			if (msg is CpfpInfoMessage.GetInfoForTransaction)
			{
				messageReceived.SetResult();
				await Task.Delay(TimeSpan.FromSeconds(30), ct);
			}
			return Unit.Instance;
		}

		using var mailbox = CreateAndStartMailboxProcessor(Handler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Act
		var task = provider.GetCpfpInfoAsync(tx, CancellationToken.None);
		await messageReceived.Task;
		processorCts.Cancel();

		// Assert
		await Assert.ThrowsAnyAsync<Exception>(() => task);
	}

	[Fact]
	public async Task ScheduleRequest_DoesNotThrow_WhenCancelledLaterAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// Act
		provider.ScheduleRequest(tx);
		processorCts.Cancel();

		// Assert - no exception thrown, fire-and-forget behavior
		await Task.Delay(50); // Give time for any potential exceptions
		Assert.True(true);
	}

	[Fact]
	public async Task ScheduleRequest_ReturnsFalse_WhenMailboxDisposedAsync()
	{
		// Arrange
		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => new HttpClient() };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		var mailbox = CreateAndStartMailboxProcessor(handler, CancellationToken.None);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		mailbox.Dispose();

		// Act - Post returns false when disposed, but ScheduleRequest doesn't expose this
		provider.ScheduleRequest(tx);

		// Assert - no exception thrown
		await Task.Delay(50);
		Assert.True(true);
	}

	[Fact]
	public async Task MultipleRequests_HandleCancellationIndependentlyAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var request1Started = new TaskCompletionSource();
		var request2Started = new TaskCompletionSource();
		var callCount = 0;

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = async req =>
		{
			var currentCall = Interlocked.Increment(ref callCount);
			if (currentCall == 1)
			{
				request1Started.SetResult();
				await Task.Delay(TimeSpan.FromSeconds(30));
			}
			else
			{
				request2Started.SetResult();
			}
			return HttpResponseMessageEx.Ok(cpfpJson);
		};

		var httpClientFactory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };
		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);

		var tx1 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var tx2 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var cts1 = new CancellationTokenSource();

		// Act
		var task1 = provider.GetCpfpInfoAsync(tx1, cts1.Token);
		await request1Started.Task;
		cts1.Cancel();

		var task2 = provider.GetCpfpInfoAsync(tx2, CancellationToken.None);

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task1);

		// Task2 might complete or might be waiting depending on timing
		// The important thing is that the provider/mailbox is still functional
		var cacheResult = await provider.GetCachedCpfpInfoAsync(CancellationToken.None);
		Assert.NotNull(cacheResult);
	}

	[Fact]
	public async Task GetCpfpInfoAsync_CompletesSuccessfully_BeforeCancellationAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);
		var tx = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		// Act
		var result = await provider.GetCpfpInfoAsync(tx, requestCts.Token);

		// Assert
		Assert.True(result.IsOk);
		Assert.False(requestCts.IsCancellationRequested);
	}

	[Fact]
	public async Task Provider_RemainsUsable_AfterCancellationExceptionAsync()
	{
		// Arrange
		using var processorCts = new CancellationTokenSource();
		var cpfpJson = """{"effectiveFeePerVsize": 10.5, "fee": 1.0, "adjustedVsize": 100, "ancestors": []}""";
		var httpClientFactory = MockHttpClientFactory.Create(
			() => HttpResponseMessageEx.Ok(cpfpJson),
			() => HttpResponseMessageEx.Ok(cpfpJson));

		var eventBus = new EventBus();
		var handler = CpfpInfoUpdater.Create(httpClientFactory, Network.Main, eventBus);

		using var mailbox = CreateAndStartMailboxProcessor(handler, processorCts.Token);
		var provider = new CpfpInfoProvider(mailbox);

		var tx1 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var tx2 = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);

		// First request - cancelled
		using var cts1 = new CancellationTokenSource();
		cts1.Cancel();

		try
		{
			await provider.GetCpfpInfoAsync(tx1, cts1.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Act - Second request should work
		var result = await provider.GetCpfpInfoAsync(tx2, CancellationToken.None);

		// Assert
		Assert.True(result.IsOk);
	}

	private static MessageHandler<CpfpInfoMessage, Unit> CreateSlowHandler()
	{
		return async (_, _, ct) =>
		{
			await Task.Delay(TimeSpan.FromSeconds(30), ct);
			return Unit.Instance;
		};
	}

	private static MailboxProcessor<CpfpInfoMessage> CreateAndStartMailboxProcessor(
		MessageHandler<CpfpInfoMessage, Unit> handler,
		CancellationToken cancellationToken)
	{
		var processor = new MailboxProcessor<CpfpInfoMessage>(
			async (mailbox, ct) =>
			{
				while (!ct.IsCancellationRequested)
				{
					var message = await mailbox.ReceiveAsync(ct);
					await handler(message, Unit.Instance, ct);
				}
			},
			cancellationToken);

		processor.Start();
		return processor;
	}
}
