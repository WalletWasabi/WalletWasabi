using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.NodeBuilding;

namespace WalletWasabi.Tests.XunitConfiguration
{
	public class RegTestFixture : IDisposable
	{
		public string BackendEndPoint { get; internal set; }
		public IWebHost BackendHost { get; internal set; }
		public CoreNode BackendRegTestNode { get; internal set; }
		public Backend.Global Global { get; }

		public RegTestFixture()
		{
			BackendRegTestNode = CoreNode.CreateAsync(EnvironmentHelpers.GetMethodName()).GetAwaiter().GetResult();

			var testnetBackendDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests", "Backend"));
			IoHelpers.DeleteRecursivelyWithMagicDustAsync(testnetBackendDir).GetAwaiter().GetResult();
			Thread.Sleep(100);
			Directory.CreateDirectory(testnetBackendDir);
			Thread.Sleep(100);
			var config = new Config(
				BackendRegTestNode.RpcClient.Network, BackendRegTestNode.RpcClient.CredentialString.ToString(),
				new IPEndPoint(IPAddress.Loopback, Network.Main.DefaultPort),
				new IPEndPoint(IPAddress.Loopback, Network.TestNet.DefaultPort),
				BackendRegTestNode.P2pEndPoint,
				new IPEndPoint(IPAddress.Loopback, Network.Main.RPCPort),
				new IPEndPoint(IPAddress.Loopback, Network.TestNet.RPCPort),
				BackendRegTestNode.RpcEndPoint);
			var configFilePath = Path.Combine(testnetBackendDir, "Config.json");
			config.SetFilePath(configFilePath);
			config.ToFileAsync().GetAwaiter().GetResult();

			var roundConfig = CreateRoundConfig(Money.Coins(0.1m), Constants.OneDayConfirmationTarget, 0.7, 0.1m, 100, 120, 60, 60, 60, 1, 24, true, 11);
			var roundConfigFilePath = Path.Combine(testnetBackendDir, "CcjRoundConfig.json");
			roundConfig.SetFilePath(roundConfigFilePath);
			roundConfig.ToFileAsync().GetAwaiter().GetResult();

			var conf = new ConfigurationBuilder()
				.AddInMemoryCollection(new[] { new KeyValuePair<string, string>("datadir", testnetBackendDir) })
				.Build();
			BackendEndPoint = $"http://localhost:{new Random().Next(37130, 38000)}/";
			BackendHost = WebHost.CreateDefaultBuilder()
					.UseStartup<Startup>()
					.UseConfiguration(conf)
					.UseWebRoot("../../../../WalletWasabi.Backend/wwwroot")
					.UseUrls(BackendEndPoint)
					.Build();
			Global = (Backend.Global)BackendHost.Services.GetService(typeof(Backend.Global));
			var hostInitializationTask = BackendHost.RunWithTasksAsync();
			Logger.LogInfo($"Started Backend webhost: {BackendEndPoint}");

			var delayTask = Task.Delay(3000);
			Task.WaitAny(delayTask, hostInitializationTask); // Wait for server to initialize (Without this OSX CI will fail)
		}

		public static CoordinatorRoundConfig CreateRoundConfig(Money denomination,
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

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					BackendHost?.StopAsync().GetAwaiter().GetResult();
					BackendHost?.Dispose();
					BackendRegTestNode?.TryKillAsync().GetAwaiter().GetResult();
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
