using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.BitcoinCore;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tor.Http;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Tests.XunitConfiguration
{
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

			var testnetBackendDir = EnvironmentHelpers.GetDataDir(Path.Combine(Common.DataDir, "RegTests", "Backend"));
			IoHelpers.TryDeleteDirectoryAsync(testnetBackendDir).GetAwaiter().GetResult();
			Thread.Sleep(100);
			Directory.CreateDirectory(testnetBackendDir);
			Thread.Sleep(100);
			var config = new Config(
				BackendRegTestNode.RpcClient.Network,
				BackendRegTestNode.RpcClient.CredentialString.ToString(),
				new IPEndPoint(IPAddress.Loopback, Network.Main.DefaultPort),
				new IPEndPoint(IPAddress.Loopback, Network.TestNet.DefaultPort),
				BackendRegTestNode.P2pEndPoint,
				new IPEndPoint(IPAddress.Loopback, Network.Main.RPCPort),
				new IPEndPoint(IPAddress.Loopback, Network.TestNet.RPCPort),
				BackendRegTestNode.RpcEndPoint);
			var configFilePath = Path.Combine(testnetBackendDir, "Config.json");
			config.SetFilePath(configFilePath);
			config.ToFile();

			var roundConfig = CreateRoundConfig(Money.Coins(0.1m), Constants.OneDayConfirmationTarget, 0.7, 0.1m, 100, 120, 60, 60, 60, 1, 24, true, 11);
			var roundConfigFilePath = Path.Combine(testnetBackendDir, "CcjRoundConfig.json");
			roundConfig.SetFilePath(roundConfigFilePath);
			roundConfig.ToFile();

			var conf = new ConfigurationBuilder()
				.AddInMemoryCollection(new[] { new KeyValuePair<string, string>("datadir", testnetBackendDir) })
				.Build();
			BackendEndPoint = $"http://localhost:{CryptoHelpers.RandomInt(37130, 37999)}/";
			BackendEndPointUri = new Uri(BackendEndPoint);
			BackendEndPointApiUri = new Uri(BackendEndPointUri, $"/api/v{Constants.BackendMajorVersion}/");

			BackendHost = Host.CreateDefaultBuilder()
					.ConfigureWebHostDefaults(webBuilder => webBuilder
							.UseStartup<Startup>()
							.UseConfiguration(conf)
							.UseWebRoot("../../../../WalletWasabi.Backend/wwwroot")
							.UseUrls(BackendEndPoint))
					.Build();

			if (BackendHost.Services.GetService(typeof(Global)) is not Global global)
			{
				throw new InvalidOperationException($"Service {nameof(Global)} is not registered.");
			}

			Global = global;
			var hostInitializationTask = BackendHost.RunWithTasksAsync();
			Logger.LogInfo($"Started Backend webhost: {BackendEndPoint}");

			HttpClient = new HttpClient();
			BackendHttpClient = new ClearnetHttpClient(HttpClient, () => BackendEndPointUri);

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

		public IHost BackendHost { get; internal set; }
		public CoreNode BackendRegTestNode { get; internal set; }
		public Global Global { get; }

		/// <summary>Underlying HTTP client to be used by <see cref="ClearnetHttpClient"/>.</summary>
		public HttpClient HttpClient { get; }

		/// <summary>Clearnet HTTP client with predefined base URI for Wasabi Backend (note: <c>/api</c> is not part of base URI).</summary>
		public ClearnetHttpClient BackendHttpClient { get; }

		public static CoordinatorRoundConfig CreateRoundConfig(
			Money denomination,
			int confirmationTarget,
			double confirmationTargetReductionRate,
			decimal coordinatorFeePercent,
			int anonymitySet,
			long inputRegistrationTimeout,
			long connectionConfirmationTimeout,
			long outputRegistrationTimeout,
			long signingTimeout,
			int dosSeverity,
			long dosDurationHours,
			bool dosNoteBeforeBan,
			int maximumMixingLevelCount)
		{
			return new CoordinatorRoundConfig
			{
				Denomination = denomination,
				ConfirmationTarget = confirmationTarget,
				ConfirmationTargetReductionRate = confirmationTargetReductionRate,
				CoordinatorFeePercent = coordinatorFeePercent,
				AnonymitySet = anonymitySet,
				InputRegistrationTimeout = inputRegistrationTimeout,
				ConnectionConfirmationTimeout = connectionConfirmationTimeout,
				SigningTimeout = signingTimeout,
				OutputRegistrationTimeout = outputRegistrationTimeout,
				DosSeverity = dosSeverity,
				DosDurationHours = dosDurationHours,
				DosNoteBeforeBan = dosNoteBeforeBan,
				MaximumMixingLevelCount = maximumMixingLevelCount,
			};
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					BackendHost?.StopAsync().GetAwaiter().GetResult();
					BackendHost?.Dispose();
					BackendRegTestNode?.TryStopAsync().GetAwaiter().GetResult();
					HttpClient.Dispose();
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

		#endregion IDisposable Support
	}
}
