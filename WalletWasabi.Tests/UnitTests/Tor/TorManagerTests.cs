using Moq;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BundledApps;
using WalletWasabi.Services;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor;

/// <summary>Tests for <see cref="TorManager"/> class.</summary>
public class TorManagerTests
{
	/// <summary>
	/// Simulates: Tor OS process is fully started. The process crashes after 2 seconds.
	/// Then we simulate then user asked to cancel TorManager as the application shuts down.
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
		mockProcess.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>()))
			.Returns((CancellationToken cancellationToken) => Task.Delay(torProcessCrashPeriod, cancellationToken));
		mockProcess.Setup(p => p.Dispose());

		// Mock TorManager.
		Mock<TorManager> mockTorProcessManager = new(MockBehavior.Strict, settings, new EventBus()) { CallBase = true };
		mockTorProcessManager.Setup(c => c.IsTorRunningAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);
		mockTorProcessManager.Setup(c => c.StartProcess(It.IsAny<string>()))
			.Returns(mockProcess.Object);
		mockTorProcessManager.Setup(c => c.EnsureRunningAsync(It.IsAny<ProcessAsync>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);
		mockTorProcessManager.SetupSequence(c => c.InitTorControlAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => new TorControlClient(pipeReader: new Pipe().Reader, pipeWriter: new Pipe().Writer)) // (1)
			.ThrowsAsync(new OperationCanceledException()); // (2)

		await using (TorManager manager = mockTorProcessManager.Object)
		{
			// No exception is expected here.
			(CancellationToken ct1, TorControlClient? client1) = await manager.StartAsync(timeoutCts.Token);

			// Wait for the Tor process crash (see (1)).
			await ct1.WhenCanceled().WaitAsync(timeoutCts.Token);

			// Wait until TorProcessManager is stopped (see (2)).
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await manager.WaitForNextAttemptAsync(timeoutCts.Token).ConfigureAwait(false));
		}

		mockTorProcessManager.Verify(c => c.StartProcess(It.IsAny<string>()), Times.Exactly(2));
		mockTorProcessManager.VerifyAll();
	}

	/// <summary>
	/// Simulates: Tor OS process is started but by a different OS user. We should throw an exception
	/// in this case as it is an unsupported scenario at the moment.
	/// </summary>
	[Theory]
	[InlineData(0)] // No process is returned as Tor is running under a different user (on linux/mac you can's see processes of other users)
	[InlineData(1)] // Single dummy Tor process is found but cannot be killed
	public async Task TorProcessStartedByDifferentUserAsync(int runningTorOsProcesses)
	{
		using CancellationTokenSource timeoutCts = new(TimeSpan.FromMinutes(2));

		// Tor settings.
		string dataDir = Path.Combine("temp", "tempDataDir");
		string distributionFolder = "tempDistributionDir";
		TorSettings settings = new(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

		Mock<TorManager> mockTorProcessManager = new(MockBehavior.Strict, settings, new EventBus()) { CallBase = true };
		mockTorProcessManager.Setup(c => c.IsTorRunningAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		mockTorProcessManager.Setup(c => c.GetTorProcesses())
			.Returns(runningTorOsProcesses == 0 ? [] : new[] { new Process() /* Dummy process */ });

		// Cookie file is stored in the profile of that different user, not ours.
		mockTorProcessManager.Setup(c => c.InitTorControlAsync(It.IsAny<CancellationToken>()))
			.ThrowsAsync(new TorControlException("Cookie file does not exist."));

		await using (TorManager torProcessManager = mockTorProcessManager.Object)
		{
			NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await torProcessManager.StartAsync(timeoutCts.Token).ConfigureAwait(false));
			Assert.Equal(TorManager.TorProcessStartedByDifferentUser, ex.Message);
		}

		mockTorProcessManager.VerifyAll();
	}
}
