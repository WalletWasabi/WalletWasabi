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
		/// Simulates: Tor OS process is fully started. The process crashes after 2 seconds.
		/// Then we simulate then user asked to cancel TorProcessManager as the application shuts down.
		/// </summary>
		[Fact]
		public async Task StartProcessAsync()
		{
			using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

			// Test parameter: Simulate that Tor process crashes after two seconds.
			TimeSpan torProcessCrashPeriod = TimeSpan.FromSeconds(2);

			// Tor settings.
			string dataDir = Path.Combine("temp", "tempDataDir");
			string distributionFolder = "tempDistributionDir";
			TorSettings settings = new(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

			// Mock Tor process.
			Mock<ProcessAsync> mockProcess = new(MockBehavior.Strict, new ProcessStartInfo());
			mockProcess.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>(), It.IsAny<bool>()))
				.Returns((CancellationToken cancellationToken, bool killOnCancel) => Task.Delay(torProcessCrashPeriod, cancellationToken));
			mockProcess.Setup(p => p.Dispose());

			// Set up Tor process manager.			
			Mock<TorTcpConnectionFactory> mockTcpConnectionFactory = new(MockBehavior.Strict, DummyTorControlEndpoint);
			mockTcpConnectionFactory.Setup(c => c.IsTorRunningAsync())
				.ReturnsAsync(false);

			// Mock TorProcessManager.
			Mock<TorProcessManager> mockTorProcessManager = new(MockBehavior.Strict, settings, mockTcpConnectionFactory.Object) { CallBase = true };
			mockTorProcessManager.Setup(c => c.StartProcess(It.IsAny<string>()))
				.Returns(mockProcess.Object);
			mockTorProcessManager.Setup(c => c.EnsureRunningAsync(It.IsAny<ProcessAsync>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(true);
			mockTorProcessManager.SetupSequence(c => c.InitTorControlAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(() => new TorControlClient(pipeReader: new Pipe().Reader, pipeWriter: new Pipe().Writer)) // (1)
				.ThrowsAsync(new OperationCanceledException()); // (2)

			await using (TorProcessManager manager = mockTorProcessManager.Object)
			{
				// No exception is expected here.
				CancellationToken ct1 = await manager.StartAsync(timeoutCts.Token);

				// Wait for the Tor process crash (see (1)).
				await ct1.WhenCanceled().WithAwaitCancellationAsync(timeoutCts.Token);

				// Wait until TorProcessManager is stopped (see (2)).
				CancellationToken ct2 = await manager.WaitForNextAttemptAsync(timeoutCts.Token);
				await ct2.WhenCanceled().WithAwaitCancellationAsync(timeoutCts.Token);
			}

			mockTorProcessManager.Verify(c => c.StartProcess(It.IsAny<string>()), Times.Exactly(2));
			mockTorProcessManager.VerifyAll();
		}
	}
}
