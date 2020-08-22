using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.ProcessBridge;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi
{
	/// <summary>
	/// The tests in this collection are time-sensitive, therefore this test collection is run in a special way:
	/// Parallel-capable test collections will be run first (in parallel), followed by parallel-disabled test collections (run sequentially) like this one.
	/// </summary>
	/// <seealso href="https://xunit.net/docs/running-tests-in-parallel.html#parallelism-in-test-frameworks"/>
	[Collection("Serial unit tests collection")]
	public class SerialDefaultResponseTests
	{
		public TimeSpan ReasonableRequestTimeout { get; } = TimeSpan.FromMinutes(1);

		[Fact]
		public async Task HwiProcessBridgeTestAsync()
		{
			HwiProcessBridge pb = new HwiProcessBridge(MicroserviceHelpers.GetBinaryPath("hwi"), new ProcessInvoker());

			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);
			var res = await pb.SendCommandAsync("version", false, cts.Token);
			Assert.NotEmpty(res.response);

			bool stdInputActionCalled = false;
			res = await pb.SendCommandAsync("version", false, cts.Token, (sw) => stdInputActionCalled = true);
			Assert.NotEmpty(res.response);
			Assert.True(stdInputActionCalled);
		}

		[Fact]
		public async void HwiProcessHelpTestAsync()
		{
			using var cts = new CancellationTokenSource();

			var processBridge = new HwiProcessBridge(MicroserviceHelpers.GetBinaryPath("hwi"), new ProcessInvoker());
			(string response, int exitCode) p = await processBridge.SendCommandAsync("--help", openConsole: false, cts.Token);

			Assert.Equal(0, p.exitCode);
			Assert.Equal("{\"error\": \"Help text requested\", \"code\": -17}" + Environment.NewLine, p.response);
		}
	}
}
