using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Http;
using WalletWasabi.WebClients.Wasabi;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Tests.XunitConfiguration;

public class RegTestFixture : IDisposable
{
	private volatile bool _disposedValue = false; // To detect redundant calls

	public RegTestFixture()
	{
		RuntimeParams.SetDataDir(Path.Combine(Common.DataDir, "RegTests", "Backend"));
		RuntimeParams.LoadAsync().GetAwaiter().GetResult();
		BackendRegTestNode = TestNodeBuilder.CreateAsync(callerFilePath: "RegTests", callerMemberName: "BitcoinCoreData").GetAwaiter().GetResult();

		var walletName = "wallet";
		BackendRegTestNode.RpcClient.CreateWalletAsync(walletName).GetAwaiter().GetResult();

		var testnetBackendDir = Path.Combine(Common.DataDir, "RegTests", "Backend");
		IoHelpers.TryDeleteDirectoryAsync(testnetBackendDir).GetAwaiter().GetResult();
		Thread.Sleep(100);
		Directory.CreateDirectory(testnetBackendDir);
		Thread.Sleep(100);
		var config = new Config(Path.Combine(testnetBackendDir, "Config.json"),
			BackendRegTestNode.RpcClient.Network,
			BackendRegTestNode.RpcClient.CredentialString.ToString(),
			new IPEndPoint(IPAddress.Loopback, Network.Main.DefaultPort),
			new IPEndPoint(IPAddress.Loopback, Network.TestNet.DefaultPort),
			BackendRegTestNode.P2pEndPoint,
			new IPEndPoint(IPAddress.Loopback, Network.Main.RPCPort),
			new IPEndPoint(IPAddress.Loopback, Network.TestNet.RPCPort),
			BackendRegTestNode.RpcEndPoint);
		config.ToFile();

		var conf = new ConfigurationBuilder()
			.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("datadir", testnetBackendDir) })
			.Build();
		BackendEndPoint = $"http://localhost:{CryptoHelpers.RandomInt(37130, 37999)}/";
		BackendEndPointUri = new Uri(BackendEndPoint);
		BackendEndPointApiUri = new Uri(BackendEndPointUri, $"api/v{Constants.BackendMajorVersion}/");

		BackendHost = Host.CreateDefaultBuilder()
				.ConfigureWebHostDefaults(webBuilder => webBuilder
						.UseStartup<Startup>()
						.UseConfiguration(conf)
						.UseUrls(BackendEndPoint))
				.Build();

		var hostInitializationTask = BackendHost.RunWithTasksAsync();
		Logger.LogInfo($"Started Backend webhost: {BackendEndPoint}");

		BackendHttpClientFactory = new BackendHttpClientFactory(BackendEndPointUri, new HttpClientFactory());

		// Wait for server to initialize
		var delayTask = Task.Delay(3000);
		Task.WaitAny(delayTask, hostInitializationTask);
	}

	/// <summary>String representation of backend URI: <c>http://localhost:RANDOM_PORT</c>.</summary>
	public string BackendEndPoint { get; }

	/// <summary>URI in form: <c>http://localhost:RANDOM_PORT</c>.</summary>
	public Uri BackendEndPointUri { get; }

	/// <summary>URI in form: <c>http://localhost:RANDOM_PORT/api/vAPI_VERSION</c>.</summary>
	public Uri BackendEndPointApiUri { get; }

	public IHost BackendHost { get; }
	public CoreNode BackendRegTestNode { get; }

	public IHttpClientFactory BackendHttpClientFactory { get; }

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				BackendHost.StopAsync().GetAwaiter().GetResult();
				BackendHost.Dispose();
				BackendRegTestNode.TryStopAsync().GetAwaiter().GetResult();
			}

			_disposedValue = true;
		}
	}

	// This code added to correctly implement the disposable pattern.
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		Dispose(true);
	}
}
