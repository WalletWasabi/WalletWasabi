using Moq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Socks5;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor
{
	/// <summary>Tests for <see cref="TorProcessManager"/> class.</summary>
	public class TorProcessManagerTests
	{
		private static readonly IPEndPoint DummyTorControlEndpoint = new(IPAddress.Loopback, 7777);

		/// <summary>
		/// Verifies that in case Tor process is successfully started and Tor Control is set up,
		/// <see cref="TorProcessManager.StartAsync(CancellationToken)"/> returns <c>true</c>.
		/// </summary>
		[Fact]
		public async Task StartProcessAsync()
		{
			using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

			// Tor settings.
			string dataDir = Path.Combine("temp", "tempDataDir");
			string distributionFolder = "tempDistributionDir";
			TorSettings settings = new(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

			// Dummy Tor process.
			Mock<ProcessAsync> mockProcess = new(MockBehavior.Strict, new ProcessStartInfo());
			mockProcess.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>(), It.IsAny<bool>()))
				.Returns(async (CancellationToken cancellationToken, bool killOnCancel) => await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false));

			// Set up Tor control client.
			Pipe toServer = new();
			Pipe toClient = new();
			await using TorControlClient controlClient = new(pipeReader: toClient.Reader, pipeWriter: toServer.Writer);

			// Set up Tor process manager.			
			Mock<TorTcpConnectionFactory> mockTcpConnectionFactory = new(MockBehavior.Strict, DummyTorControlEndpoint);
			mockTcpConnectionFactory.Setup(c => c.IsTorRunningAsync())
				.ReturnsAsync(false);

			Mock<TorProcessManager> mockTorProcessManager = new(MockBehavior.Strict, settings, mockTcpConnectionFactory.Object) { CallBase = true };
			mockTorProcessManager.Setup(c => c.StartProcess(It.IsAny<string>()))
				.Returns(mockProcess.Object);
			mockTorProcessManager.Setup(c => c.EnsureRunningAsync(It.IsAny<ProcessAsync>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(true);
			mockTorProcessManager.Setup(c => c.InitTorControlAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(controlClient);

			TorProcessManager manager = mockTorProcessManager.Object;
			bool result = await manager.StartAsync(timeoutCts.Token);
			Assert.True(result);
		}
	}
}
