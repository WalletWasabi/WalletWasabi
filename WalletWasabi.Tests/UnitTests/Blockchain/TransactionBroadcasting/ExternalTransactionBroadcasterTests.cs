using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionBroadcasting;

public class ExternalTransactionBroadcasterTests
{
	[Fact]
	public async Task BroadcastAsync_Success_ReturnsOkWithProviderName()
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		var factory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		var result = await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.True(result.IsOk);
		var ok = Assert.IsType<BroadcastOk.BroadcastByExternalParty>(result.Value);
		Assert.Equal(providerInfo.Name, ok.ExternalApiName);
	}

	[Fact]
	public async Task BroadcastAsync_UsesClearNetUrl_ForNonOnionFactory()
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];
		Uri? capturedUri = null;

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = request =>
		{
			capturedUri = request.RequestUri;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		};
		var factory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.NotNull(capturedUri);
		Assert.Contains(providerInfo.ApiDomain.ClearNet.TrimEnd('/'), capturedUri.ToString());
		Assert.DoesNotContain(".onion", capturedUri.ToString());
	}

	[Fact]
	public async Task BroadcastAsync_PostsTransactionHex()
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];
		string? capturedContent = null;

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = async request =>
		{
			capturedContent = await request.Content!.ReadAsStringAsync();
			return new HttpResponseMessage(HttpStatusCode.OK);
		};
		var factory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.NotNull(capturedContent);
		Assert.Equal(tx.Transaction.ToHex(), capturedContent);
	}

	[Fact]
	public async Task BroadcastAsync_SetsUserAgentHeader()
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];
		HttpClient? capturedClient = null;

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		var factory = new MockHttpClientFactory
		{
			OnCreateClient = _ =>
			{
				capturedClient = mockHttpClient;
				return mockHttpClient;
			}
		};

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.NotNull(capturedClient);
		Assert.True(capturedClient.DefaultRequestHeaders.Contains("User-Agent"));
	}

	[Theory]
	[InlineData(RpcErrorTools.SpentError1)]
	[InlineData(RpcErrorTools.SpentError2)]
	[InlineData(RpcErrorTools.SpentError3)]
	public async Task BroadcastAsync_HttpRequestExceptionWithSpentError_ReturnsSpentError(string spentErrorMessage)
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ => throw new HttpRequestException($"Error: {spentErrorMessage}");
		var factory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		var result = await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.False(result.IsOk);
		Assert.IsType<BroadcastError.SpentError>(result.Error);
	}

	[Fact]
	public async Task BroadcastAsync_HttpRequestException_ReturnsUnknownError()
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];
		var errorMessage = "Connection refused";

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ => throw new HttpRequestException(errorMessage);
		var factory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		var result = await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.False(result.IsOk);
		var error = Assert.IsType<BroadcastError.Unknown>(result.Error);
		Assert.Equal(errorMessage, error.Message);
	}

	[Fact]
	public async Task BroadcastAsync_GeneralException_ReturnsUnknownError()
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];
		var errorMessage = "Unexpected error";

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ => throw new InvalidOperationException(errorMessage);
		var factory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		var result = await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.False(result.IsOk);
		var error = Assert.IsType<BroadcastError.Unknown>(result.Error);
		Assert.Equal(errorMessage, error.Message);
	}

	[Fact]
	public async Task BroadcastAsync_HttpError_ReturnsUnknownError()
	{
		var tx = BitcoinFactory.CreateSmartTransaction();
		var providerInfo = ExternalTransactionBroadcaster.Providers[0];

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
		{
			ReasonPhrase = "Internal Server Error"
		});
		var factory = new MockHttpClientFactory { OnCreateClient = _ => mockHttpClient };

		var broadcaster = new ExternalTransactionBroadcaster(providerInfo, factory);
		var result = await broadcaster.BroadcastAsync(tx, CancellationToken.None);

		Assert.False(result.IsOk);
		Assert.IsType<BroadcastError.Unknown>(result.Error);
	}
}
