using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Payjoin;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// The OHTTP-keys bootstrap must never reveal the client IP to the directory, and every byte
/// must ride Wasabi's own HTTP transport — the payjoin-ffi is reduced to the pure
/// <c>OhttpKeys.Decode</c> parser and never owns an <see cref="HttpClient"/>. With Tor enabled
/// the keys are fetched straight from the directory through the (Tor-riding) factory; with Tor
/// disabled they are fetched through a relay set as a CONNECT proxy, built from Wasabi's own
/// <see cref="WalletWasabi.WebClients.Wasabi.RelayHttpClientFactory"/>.
/// </summary>
public class PayjoinOhttpBootstrapTests
{
	private class RecordingHandler : HttpMessageHandler
	{
		public List<HttpRequestMessage> Requests { get; } = new();

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(request);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new ByteArrayContent(Bip77PayjoinTests.TestOhttpKeys),
			});
		}
	}

	private class StubHttpClientFactory : IHttpClientFactory
	{
		private readonly HttpMessageHandler _handler;

		public StubHttpClientFactory(HttpMessageHandler handler)
		{
			_handler = handler;
		}

		public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
	}

	/// <summary>Captures the relay URI PayjoinManager hands the relay-factory builder and records
	/// the bootstrap requests, so a test can prove the Tor-off path rides this injected factory.</summary>
	private sealed class RecordingRelayFactoryBuilder
	{
		public List<Uri> RelayUris { get; } = new();
		public RecordingHandler Handler { get; } = new();

		public IHttpClientFactory Build(Uri relayUri)
		{
			RelayUris.Add(relayUri);
			return new StubHttpClientFactory(Handler);
		}
	}

	private static PayjoinManager CreateManager(
		string dataDir,
		IHttpClientFactory httpClientFactory,
		bool torEnabled,
		string[]? relays = null,
		Func<Uri, IHttpClientFactory>? relayHttpClientFactoryBuilder = null)
	{
		var configuration = new PayjoinConfiguration(
			DirectoryUrl: "https://payjo.in",
			OhttpRelayUrls: relays ?? ["https://relay.example"],
			MaxFeeRateSatPerVb: 1000,
			TorEnabled: torEnabled);

#pragma warning disable CA2000 // Ownership of the broadcaster's mempool service ends with the test process.
		return new PayjoinManager(
			dataDir,
			Network.Main,
			configuration,
			() => Task.FromResult(Enumerable.Empty<WalletWasabi.Wallets.Wallet>()),
			httpClientFactory,
			new TransactionBroadcaster([], new MempoolService(new EventBus())),
			relayHttpClientFactoryBuilder);
#pragma warning restore CA2000
	}

	[Fact]
	public async Task TorEnabled_FetchesKeysFromDirectoryThroughFactory()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		using var handler = new RecordingHandler();
		var relayBuilder = new RecordingRelayFactoryBuilder();
		using PayjoinManager manager = CreateManager(
			workDir, new StubHttpClientFactory(handler), torEnabled: true, relayHttpClientFactoryBuilder: relayBuilder.Build);

		using var keys = await manager.GetOhttpKeysAsync(CancellationToken.None);

		Assert.NotNull(keys);

		// The Tor-on invariant this test locks: the fetch rides the injected (Tor-riding) factory
		// and hits the directory's well-known endpoint directly. If the Tor branch were ever rewired
		// to build its own HttpClient (the retired payjoin-ffi OhttpKeysClient, or the relay factory),
		// this handler would record nothing and the assertions below would go red.
		HttpRequestMessage request = Assert.Single(handler.Requests);
		Assert.Equal(HttpMethod.Get, request.Method);
		Assert.Equal("https://payjo.in/.well-known/ohttp-gateway", request.RequestUri?.ToString());

		// The relay (Tor-off) path must not be touched at all when Tor is enabled.
		Assert.Empty(relayBuilder.RelayUris);
		Assert.Empty(relayBuilder.Handler.Requests);

		// Second fetch is served from the 12 h cache without another request.
		using var cachedKeys = await manager.GetOhttpKeysAsync(CancellationToken.None);
		Assert.Single(handler.Requests);
	}

	[Fact]
	public async Task TorDisabled_FetchesKeysThroughRelayProxyFactory()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		using var directoryHandler = new RecordingHandler();
		var relayBuilder = new RecordingRelayFactoryBuilder();

		using PayjoinManager manager = CreateManager(
			workDir,
			new StubHttpClientFactory(directoryHandler),
			torEnabled: false,
			relays: ["https://relay.example"],
			relayHttpClientFactoryBuilder: relayBuilder.Build);

		using var keys = await manager.GetOhttpKeysAsync(CancellationToken.None);

		Assert.NotNull(keys);

		// The bootstrap rode Wasabi's relay factory, built for the configured relay — proving the
		// directory never sees the client IP directly and the payjoin-ffi never owned transport.
		Uri relayUri = Assert.Single(relayBuilder.RelayUris);
		Assert.Equal("https://relay.example/", relayUri.ToString());
		HttpRequestMessage request = Assert.Single(relayBuilder.Handler.Requests);
		Assert.Equal(HttpMethod.Get, request.Method);
		Assert.Equal("https://payjo.in/.well-known/ohttp-gateway", request.RequestUri?.ToString());

		// The plain injected (non-relay) factory must never carry the clearnet bootstrap: routing it
		// there would connect straight to the directory and leak the client IP.
		Assert.Empty(directoryHandler.Requests);
	}

	[Fact]
	public async Task TorDisabled_RotatesToNextRelayOnTransportError()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		using var directoryHandler = new RecordingHandler();

		// First relay's factory always fails at the transport; the second succeeds. The rotation
		// must reach the second relay rather than surfacing the first failure.
		var attempts = new List<Uri>();
		using var okHandler = new RecordingHandler();
		using var throwingHandler = new ThrowingHandler();
		IHttpClientFactory RelayBuilder(Uri relayUri)
		{
			attempts.Add(relayUri);
			return relayUri.Host == "bad.example"
				? new StubHttpClientFactory(throwingHandler)
				: new StubHttpClientFactory(okHandler);
		}

		using PayjoinManager manager = CreateManager(
			workDir,
			new StubHttpClientFactory(directoryHandler),
			torEnabled: false,
			relays: ["https://bad.example", "https://good.example"],
			relayHttpClientFactoryBuilder: RelayBuilder);

		using var keys = await manager.GetOhttpKeysAsync(CancellationToken.None);

		Assert.NotNull(keys);
		Assert.Contains(new Uri("https://good.example"), attempts);
		Assert.Single(okHandler.Requests);
		Assert.Empty(directoryHandler.Requests);
	}

	private sealed class ThrowingHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			throw new HttpRequestException("relay unreachable");
	}
}
