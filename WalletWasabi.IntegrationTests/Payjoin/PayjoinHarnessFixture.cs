using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Helpers;
using WalletWasabi.IntegrationTests.BitcoinCore.Endpointing;
using Xunit;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// Hermetic, loopback-only environment for payjoin harness tests: a dedicated regtest bitcoind
/// (spawned in the BitcoindRpcProcessBridge pattern), a payjoin-mailroom directory instance and a
/// separate mailroom OHTTP relay instance, all on ephemeral ports with per-run temp directories.
/// A "bank" wallet mines the initial chain and funds per-test wallets so tests never wait for
/// coinbase maturity.
/// </summary>
public sealed class PayjoinHarnessFixture : IAsyncLifetime
{
	private const string RpcUserName = "payjoinharness";
	private const string RpcPasswordValue = "payjoinharness";

	private LineBufferedProcess? _bitcoind;
	private MailroomProcess? _directory;
	private MailroomProcess? _relay;
	private PayjoinTestServicesProcess? _tlsServices;
	private RPCClient? _bankRpc;

	public PayjoinHarnessFixture()
	{
		RootDir = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "IntegrationTests")), "PayjoinHarness", Guid.NewGuid().ToString("N"));

		// The test process may inherit http_proxy from the host (a sandbox proxy 403s
		// loopback targets), and .NET's default proxy honors it — which would break NBitcoin's
		// RPC calls. All harness traffic is loopback, so drop the process-wide default proxy.
		System.Net.Http.HttpClient.DefaultProxy = new System.Net.WebProxy();

		// Loopback-only HttpClient that must not go through any host proxy.
#pragma warning disable CA2000 // Dispose objects before losing scope - handler ownership transferred to HttpClient
		HttpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false }, disposeHandler: true);
#pragma warning restore CA2000
	}

	public string RootDir { get; }
	public HttpClient HttpClient { get; }
	public int RpcPort { get; private set; }
	public string RpcUser => RpcUserName;
	public string RpcPassword => RpcPasswordValue;
	public MailroomProcess Directory => _directory ?? throw new InvalidOperationException("Fixture not initialized.");
	public MailroomProcess Relay => _relay ?? throw new InvalidOperationException("Fixture not initialized.");
	public string OhttpKeysPath => Path.Combine(RootDir, "ohttp-keys.bin");
	public RPCClient BankRpc => _bankRpc ?? throw new InvalidOperationException("Fixture not initialized.");

	/// <summary>TLS topology: TestServices via the contrib/payjoin-fixture shim (self-signed https directory + relays trusting it).</summary>
	public PayjoinTestServicesProcess TlsServices => _tlsServices ?? throw new InvalidOperationException("Fixture not initialized.");

	/// <summary>
	/// HttpClient trusting exactly the TLS fixture's self-signed certificate (pinned by DER bytes) —
	/// the same shape Wasabi's production code needs for its OHTTP transport over https.
	/// Pass a relay URL as <paramref name="proxyUrl"/> to tunnel https requests through the OHTTP
	/// relay via CONNECT, the RFC 9540 bootstrap transport.
	/// </summary>
	public HttpClient CreateTlsPinnedHttpClient(string? proxyUrl = null)
	{
		byte[] pinnedDer = TlsServices.CertificateDer;
#pragma warning disable CA2000 // Dispose objects before losing scope - handler ownership transferred to HttpClient
		var handler = new HttpClientHandler
		{
			ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && cert.RawData.AsSpan().SequenceEqual(pinnedDer),
		};
		if (proxyUrl is not null)
		{
			handler.Proxy = new System.Net.WebProxy(proxyUrl);
			handler.UseProxy = true;
		}
		else
		{
			handler.UseProxy = false;
		}

		return new HttpClient(handler, disposeHandler: true);
#pragma warning restore CA2000
	}

	public async ValueTask InitializeAsync()
	{
		System.IO.Directory.CreateDirectory(RootDir);

		int[] ports = PortFinder.GetRandomPorts(2);
		RpcPort = ports[0];
		string dataDir = Path.Combine(RootDir, "bitcoind");
		System.IO.Directory.CreateDirectory(dataDir);

		_bitcoind = LineBufferedProcess.Start(
			HarnessBinaries.BitcoindPath,
			[
				"-regtest",
				$"-datadir={dataDir}",
				$"-rpcport={RpcPort}",
				$"-port={ports[1]}",
				$"-rpcuser={RpcUserName}",
				$"-rpcpassword={RpcPasswordValue}",
				"-fallbackfee=0.0002",
				"-listen=0",
				"-txindex=1",
				"-printtoconsole=1",
			],
			RootDir);

		var nodeRpc = new RPCClient($"{RpcUserName}:{RpcPasswordValue}", new Uri($"http://127.0.0.1:{RpcPort}"), Network.RegTest);
		await WaitForRpcAsync(nodeRpc).ConfigureAwait(false);

		await nodeRpc.SendCommandAsync("createwallet", "bank").ConfigureAwait(false);
		_bankRpc = CreateWalletRpc("bank");

		// 110 blocks -> ten mature 50 BTC coinbases for funding per-test wallets.
		BitcoinAddress bankAddress = await _bankRpc.GetNewAddressAsync().ConfigureAwait(false);
		await _bankRpc.GenerateToAddressAsync(110, bankAddress).ConfigureAwait(false);

		_directory = await MailroomProcess.StartAsync(Path.Combine(RootDir, "directory"), enableV1: true, HttpClient).ConfigureAwait(false);
		_relay = await MailroomProcess.StartAsync(Path.Combine(RootDir, "relay"), enableV1: false, HttpClient).ConfigureAwait(false);
		await _directory.FetchOhttpKeysAsync(OhttpKeysPath, HttpClient).ConfigureAwait(false);

		_tlsServices = await PayjoinTestServicesProcess.StartAsync(Path.Combine(RootDir, "tls-services")).ConfigureAwait(false);
	}

	/// <summary>Creates a fresh wallet holding one confirmed non-coinbase UTXO of the given amount.</summary>
	public async Task<RPCClient> CreateFundedWalletAsync(string walletName, Money amount)
	{
		await BankRpc.SendCommandAsync("createwallet", walletName).ConfigureAwait(false);
		RPCClient walletRpc = CreateWalletRpc(walletName);
		BitcoinAddress address = await walletRpc.GetNewAddressAsync().ConfigureAwait(false);
		await BankRpc.SendToAddressAsync(address, amount).ConfigureAwait(false);
		await MineAsync(1).ConfigureAwait(false);
		return walletRpc;
	}

	public async Task MineAsync(int blockCount)
	{
		BitcoinAddress address = await BankRpc.GetNewAddressAsync().ConfigureAwait(false);
		await BankRpc.GenerateToAddressAsync(blockCount, address).ConfigureAwait(false);
	}

	public string GetWalletRpcUrl(string walletName)
	{
		return $"http://127.0.0.1:{RpcPort.ToString(CultureInfo.InvariantCulture)}/wallet/{walletName}";
	}

	public RPCClient CreateWalletRpc(string walletName)
	{
		return new RPCClient($"{RpcUserName}:{RpcPasswordValue}", new Uri(GetWalletRpcUrl(walletName)), Network.RegTest);
	}

	public string CreateDriverWorkDir(string name)
	{
		string path = Path.Combine(RootDir, "cli", $"{name}-{Guid.NewGuid():N}");
		System.IO.Directory.CreateDirectory(path);
		return path;
	}

	public async ValueTask DisposeAsync()
	{
		_directory?.Dispose();
		_relay?.Dispose();
		_tlsServices?.Dispose();

		if (_bitcoind is not null)
		{
			try
			{
				var nodeRpc = new RPCClient($"{RpcUserName}:{RpcPasswordValue}", new Uri($"http://127.0.0.1:{RpcPort}"), Network.RegTest);
				await nodeRpc.SendCommandAsync("stop").ConfigureAwait(false);
				await _bitcoind.WaitForExitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Graceful shutdown failed; fall through to kill.
			}

			_bitcoind.Dispose();
		}

		HttpClient.Dispose();

		try
		{
			if (System.IO.Directory.Exists(RootDir))
			{
				System.IO.Directory.Delete(RootDir, recursive: true);
			}
		}
		catch (IOException)
		{
			// Best-effort temp cleanup.
		}
	}

	private async Task WaitForRpcAsync(RPCClient rpc)
	{
		DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
		while (true)
		{
			try
			{
				await rpc.GetBlockchainInfoAsync().ConfigureAwait(false);
				return;
			}
			catch (Exception ex)
			{
				if (_bitcoind is { HasExited: true })
				{
					throw new InvalidOperationException($"bitcoind exited during startup.{_bitcoind.DescribeBuffers()}", ex);
				}

				if (DateTime.UtcNow >= deadline)
				{
					throw new TimeoutException($"bitcoind RPC did not come up within 30s.{_bitcoind?.DescribeBuffers()}", ex);
				}

				await Task.Delay(200).ConfigureAwait(false);
			}
		}
	}
}
