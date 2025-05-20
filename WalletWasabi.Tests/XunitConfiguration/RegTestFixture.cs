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
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.BitcoinCore;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WebClients.Wasabi;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Tests.XunitConfiguration;

public class RegTestFixture : IDisposable
{
	private volatile bool _disposedValue = false; // To detect redundant calls

	public RegTestFixture()
	{
		IndexerRegTestNode = TestNodeBuilder.CreateAsync(callerFilePath: "RegTests", callerMemberName: "BitcoinCoreData").GetAwaiter().GetResult();

		var walletName = "wallet";
		IndexerRegTestNode.RpcClient.CreateWalletAsync(walletName).GetAwaiter().GetResult();

		var testnetBackendDir = Path.Combine(Common.DataDir, "RegTests", "Backend");
		IoHelpers.TryDeleteDirectoryAsync(testnetBackendDir).GetAwaiter().GetResult();
		Thread.Sleep(100);
		Directory.CreateDirectory(testnetBackendDir);
		Thread.Sleep(100);
		var config = new Config(Path.Combine(testnetBackendDir, "Config.json"),
			IndexerRegTestNode.RpcClient.Network,
			IndexerRegTestNode.RpcClient.CredentialString.ToString(),
			$"http://localhost:{Network.Main.RPCPort}",
			$"http://localhost:{Network.TestNet.RPCPort}",
			$"http://{IndexerRegTestNode.RpcEndPoint}",
			Constants.DefaultFilterType
			);
		config.ToFile();

		var conf = new ConfigurationBuilder()
			.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("datadir", testnetBackendDir) })
			.Build();
		IndexerEndPoint = $"http://localhost:{CryptoHelpers.RandomInt(37130, 37999)}/";
		IndexerEndPointUri = new Uri(IndexerEndPoint);

		IndexerHost = Host.CreateDefaultBuilder()
				.ConfigureWebHostDefaults(webBuilder => webBuilder
						.UseStartup<Startup>()
						.UseConfiguration(conf)
						.UseUrls(IndexerEndPoint))
				.Build();

		var hostInitializationTask = IndexerHost.RunWithTasksAsync();
		Logger.LogInfo($"Started Indexer webhost: {IndexerEndPoint}");

		IndexerHttpClientFactory = new IndexerHttpClientFactory(IndexerEndPointUri, new HttpClientFactory());

		// Wait for server to initialize
		var delayTask = Task.Delay(3000);
		Task.WaitAny(delayTask, hostInitializationTask);
	}

	/// <summary>String representation of indexer URI: <c>http://localhost:RANDOM_PORT</c>.</summary>
	public string IndexerEndPoint { get; }

	/// <summary>URI in form: <c>http://localhost:RANDOM_PORT</c>.</summary>
	public Uri IndexerEndPointUri { get; }

	public IHost IndexerHost { get; }
	public CoreNode IndexerRegTestNode { get; }

	public IHttpClientFactory IndexerHttpClientFactory { get; }

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				IndexerHost.StopAsync().GetAwaiter().GetResult();
				IndexerHost.Dispose();
				IndexerRegTestNode.TryStopAsync().GetAwaiter().GetResult();
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
