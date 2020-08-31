using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.ProcessBridge;
using WalletWasabi.Microservices;
using Xunit;
using WalletWasabi.Helpers;

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

		/// <summary>Verify that <c>--version</c> argument returns output as expected.</summary>
		[Fact]
		public async Task HwiVersionTestAsync()
		{
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);

			var pb = new HwiProcessBridge(new ProcessInvoker());

			// Start HWI with "version" argument and test that we get non-empty response.
			(string response, int exitCode) result = await pb.SendCommandAsync("--version", openConsole: false, cts.Token);
			Assert.Contains(Constants.HwiVersion.ToString(), result.response);

			// Start HWI with "version" argument and test that we get non-empty response + verify that "standardInputWriter" is actually called.
			bool stdInputActionCalled = false;
			result = await pb.SendCommandAsync("--version", openConsole: false, cts.Token, (sw) => stdInputActionCalled = true);
			Assert.Contains(Constants.HwiVersion.ToString(), result.response);
			Assert.True(stdInputActionCalled);
		}

		/// <summary>Verify that <c>--help</c> returns output as expected.</summary>
		[Fact]
		public async void HwiHelpTestAsync()
		{
			using var cts = new CancellationTokenSource(ReasonableRequestTimeout);

			var processBridge = new HwiProcessBridge(new ProcessInvoker());
			(string response, int exitCode) result = await processBridge.SendCommandAsync("--help", openConsole: false, cts.Token);

			Assert.Equal(0, result.exitCode);
			Assert.Equal(@"{""error"": ""Help text requested"", ""code"": -17}" + Environment.NewLine, result.response);
		}
	}
}
