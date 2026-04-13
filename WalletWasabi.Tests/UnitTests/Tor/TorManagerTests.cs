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

		// Tor settings.
		string dataDir = Path.Combine("temp", "tempDataDir");
		string distributionFolder = "tempDistributionDir";
		TorSettings settings = new(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

		var processManager = new TestProcessManager(settings, new EventBus())
		{
			// Simulate that Tor process crashes after two seconds.
			WaitForTorProcessDelay = TimeSpan.FromSeconds(2),
			IsTorRunningAsyncResult = false,
			EnsureRunningAsyncResult = true,
			InitTorControlAsyncResult = new TorControlClient(pipeReader: new Pipe().Reader, pipeWriter: new Pipe().Writer)  // (1)
		};

		await using (TorManager manager = new(settings, processManager))
		{
			// No exception is expected here.
			(CancellationToken ct1, TorControlClient? client1) = await manager.StartAsync(timeoutCts.Token);

			// Wait for the Tor process crash (see (1)).
			await ct1.WhenCanceled().WaitAsync(timeoutCts.Token);

			// On second attempt, InitTorControlAsync should throw.
			processManager.InitTorControlAsyncException = new OperationCanceledException(); // (2)

			// Wait until TorProcessManager is stopped (see (2)).
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await manager.WaitForNextAttemptAsync(timeoutCts.Token).ConfigureAwait(false));
		}

		Assert.Equal(2, processManager.StartProcessCallCount);
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

		var processManager = new TestProcessManager(settings, new EventBus())
		{
			IsTorRunningAsyncResult = true,
			GetTorProcessesResult = runningTorOsProcesses == 0 ? [] : new[] { new Process() /* Dummy process */ },

			// Cookie file is stored in the profile of that different user, not ours.
			InitTorControlAsyncException = new TorControlException("Cookie file does not exist.")
		};

		await using TorManager torManager = new(settings, processManager);

		NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
			async () => await torManager.StartAsync(timeoutCts.Token).ConfigureAwait(false));
		Assert.Equal(TorManager.TorProcessStartedByDifferentUser, ex.Message);
	}

	class TestProcessManager : ProcessManager
	{
		public int StartProcessCallCount { get; private set; }
		public TimeSpan? WaitForTorProcessDelay { get; set; }
		public bool? IsTorRunningAsyncResult { get; set; }
		public bool? EnsureRunningAsyncResult { get; set; }
		public Process[]? GetTorProcessesResult { get; set; }
		public TorControlClient? InitTorControlAsyncResult { get; set; }
		public Exception? InitTorControlAsyncException { get; set; }

		public TestProcessManager(TorSettings settings, EventBus eventBus)
			: base(settings, eventBus)
		{
		}

		public override ProcessAsync StartProcess(string arguments)
		{
			StartProcessCallCount++;
			return base.StartProcess(arguments);
		}

		public override async Task WaitForProcessExitAsync(ProcessAsync process, CancellationToken cancellationToken)
		{
			if (WaitForTorProcessDelay is not null)
			{
				await Task.Delay(WaitForTorProcessDelay.Value, cancellationToken);
				return;
			}

			await base.WaitForProcessExitAsync(process, cancellationToken);
		}

		public override async Task<bool> IsTorRunningAsync(CancellationToken cancellationToken)
		{
			if (IsTorRunningAsyncResult is not null)
			{
				return IsTorRunningAsyncResult.Value;
			}

			return await base.IsTorRunningAsync(cancellationToken).ConfigureAwait(false);
		}

		public override async Task<bool> EnsureRunningAsync(ProcessAsync process, CancellationToken cancellationToken)
		{
			if (EnsureRunningAsyncResult is not null)
			{
				return EnsureRunningAsyncResult.Value;
			}

			return await base.EnsureRunningAsync(process, cancellationToken).ConfigureAwait(false);
		}

		public override Process[] GetTorProcesses()
		{
			if (GetTorProcessesResult is not null)
			{
				return GetTorProcessesResult;
			}

			return base.GetTorProcesses();
		}

		public override async Task<TorControlClient> InitTorControlAsync(CancellationToken cancellationToken)
		{
			if (InitTorControlAsyncException is not null)
			{
				throw InitTorControlAsyncException;
			}

			if (InitTorControlAsyncResult is not null)
			{
				return InitTorControlAsyncResult;
			}

			return await base.InitTorControlAsync(cancellationToken).ConfigureAwait(false);
		}
	}
}
