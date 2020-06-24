using System;
using System.Threading;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Microservices
{
	public class ProcessBridgeTest
	{
		[Fact]
		public async void SendCommandSuccessAsync()
		{
			using var cts = new CancellationTokenSource();

			var processBridge = new ProcessBridge(MicroserviceHelpers.GetBinaryPath("hwi"));
			(string response, int exitCode) p = await processBridge.SendCommandAsync("--help", openConsole: false, cts.Token);

			Assert.Equal(0, p.exitCode);
			Assert.Equal("{\"error\": \"Help text requested\", \"code\": -17}" + Environment.NewLine, p.response);
		}
	}
}
