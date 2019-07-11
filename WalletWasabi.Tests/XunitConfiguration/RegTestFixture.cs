using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Tests.NodeBuilding;

namespace WalletWasabi.Tests.XunitConfiguration
{
	public class RegTestFixture : IDisposable
	{
		public string BackendEndPoint { get; internal set; }

		public IWebHost BackendHost { get; internal set; }

		public NodeBuilder BackendNodeBuilder { get; internal set; }

		public CoreNode BackendRegTestNode { get; internal set; }

		public RegTestFixture()
		{
			BackendNodeBuilder = NodeBuilder.CreateAsync(nameof(RegTestFixture)).GetAwaiter().GetResult();
			BackendNodeBuilder.CreateNodeAsync().GetAwaiter().GetResult();
			BackendNodeBuilder.StartAllAsync().GetAwaiter().GetResult();
			BackendRegTestNode = BackendNodeBuilder.Nodes[0];

			var rpc = BackendRegTestNode.CreateRpcClient();

			var config = new Config(rpc.Network, rpc.Authentication, IPAddress.Loopback.ToString(), IPAddress.Loopback.ToString(), BackendRegTestNode.Endpoint.Address.ToString(), Network.Main.DefaultPort, Network.TestNet.DefaultPort, BackendRegTestNode.Endpoint.Port);

			var roundConfig = CreateRoundConfig(Money.Coins(0.1m), Constants.OneDayConfirmationTarget, 0.7, 0.1m, 100, 120, 60, 60, 60, 1, 24, true, 11);

			Backend.Global.Instance.InitializeAsync(config, roundConfig, rpc).GetAwaiter().GetResult();

			BackendEndPoint = $"http://localhost:{new Random().Next(37130, 38000)}/";
			BackendHost = WebHost.CreateDefaultBuilder()
					.UseStartup<Startup>()
					.UseUrls(BackendEndPoint)
					.Build();

			var hostInitializationTask = BackendHost.RunAsync();
			Logger.LogInfo($"Started Backend webhost: {BackendEndPoint}", nameof(Global));

			var delayTask = Task.Delay(3000);
			Task.WaitAny(delayTask, hostInitializationTask); // Wait for server to initialize (Without this OSX CI will fail)
		}

		public static CcjRoundConfig CreateRoundConfig(Money denomination,
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
			return new CcjRoundConfig
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

		public void Dispose()
		{
			// Cleanup tests...

			BackendHost?.StopAsync().GetAwaiter().GetResult();
			BackendHost?.Dispose();
			BackendRegTestNode?.TryKillAsync(cleanFolder: true).GetAwaiter().GetResult();
			BackendNodeBuilder?.Dispose();
		}
	}
}
