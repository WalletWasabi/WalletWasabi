using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor;

/// <summary>
/// Tests for <see cref="TorManager"/> class.
/// </summary>
public class TorManagerTests
{
	/// <summary>
	/// Simulates: Tor OS process is fully started. The process crashes after 2 seconds.
	/// Then we simulate then user asked to cancel TorManager as the application shuts down.
	/// </summary>
	[Fact(Timeout = 120_000)]
	public async Task StartProcessAsync()
	{
		// Tor settings.
		string dataDir = Path.Combine("temp", "tempDataDir");
		string distributionFolder = "tempDistributionDir";
		TorSettings settings = new(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

		var processManager = new TestProcessManager(new EventBus())
		{
			// Simulate that Tor process crashes after two seconds.
			WaitForTorProcessDelay = TimeSpan.FromSeconds(2),
			IsTorRunningAsyncResult = false,
			EnsureRunningAsyncResult = true,
			InitTorControlAsyncResult = new TorControlClient(pipeReader: new Pipe().Reader, pipeWriter: new Pipe().Writer) // (1)
		};

		await using (TorManager manager = new(settings, processManager))
		{
			// No exception is expected here.
			(CancellationToken ct1, TorControlClient? client1) = await manager.StartAsync(TestContext.Current.CancellationToken);

			// Wait for the Tor process crash (see (1)).
			await ct1.WhenCanceled().WaitAsync(TestContext.Current.CancellationToken);

			// On second attempt, InitTorControlAsync should throw.
			processManager.InitTorControlAsyncException = new OperationCanceledException(); // (2)

			// Wait until TorProcessManager is stopped (see (2)).
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				async () => await manager.WaitForNextAttemptAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));
		}

		Assert.Equal(2, processManager.StartProcessCallCount);
	}

	/// <summary>
	/// Simulates: Tor OS process starts but its SOCKS5 port never becomes available (e.g. a stale Tor process blocks the port).
	/// We should give up after the specified number of attempts, explain why the last attempt failed and not leave Tor processes behind.
	/// </summary>
	[Fact(Timeout = 120_000)]
	public async Task TorFailsToStartAsync()
	{
		// Tor settings.
		string dataDir = Path.Combine("temp", "tempDataDir");
		string distributionFolder = "tempDistributionDir";
		TorSettings settings = new(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

		var processManager = new TestProcessManager(new EventBus())
		{
			IsTorRunningAsyncResult = false,
			EnsureRunningAsyncResult = false,
		};

		InvalidOperationException ex;

		await using (TorManager manager = new(settings, processManager))
		{
			ex = await Assert.ThrowsAsync<InvalidOperationException>(
				async () => await manager.StartAsync(attempts: 2, TestContext.Current.CancellationToken).ConfigureAwait(false));
		}

		Assert.StartsWith(TorManager.NoAttemptToStartTorWasSuccessful, ex.Message);
		Assert.Contains("Tor process", ex.Message);

		// The manager keeps trying to start Tor until it is disposed, so there can be more attempts than requested.
		Assert.True(processManager.StartProcessCallCount >= 2);

		// No Tor process started by the manager may be left behind.
		foreach (int processId in processManager.StartedProcessIds)
		{
			await WaitUntilProcessIsGoneAsync(processId, TestContext.Current.CancellationToken);
		}
	}

	private static async Task WaitUntilProcessIsGoneAsync(int processId, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

		while (true)
		{
			try
			{
				using Process process = Process.GetProcessById(processId);

				if (process.HasExited)
				{
					return;
				}
			}
			catch (ArgumentException)
			{
				// No such process.
				return;
			}

			await Task.Delay(100, timeoutCts.Token);
		}
	}

	/// <summary>
	/// Simulates: Tor OS process is started but by a different OS user. We should throw an exception
	/// in this case as it is an unsupported scenario at the moment.
	/// </summary>
	[Theory(Timeout = 120_000)]
	[InlineData(0)] // No process is returned as Tor is running under a different user (on linux/mac you can's see processes of other users)
	[InlineData(1)] // Single dummy Tor process is found but cannot be killed
	public async Task TorProcessStartedByDifferentUserAsync(int runningTorOsProcesses)
	{
		// Tor settings.
		string dataDir = Path.Combine("temp", "tempDataDir");
		string distributionFolder = "tempDistributionDir";
		TorSettings settings = new(dataDir, distributionFolder, terminateOnExit: true, owningProcessId: 7);

		var processManager = new TestProcessManager(new EventBus())
		{
			IsTorRunningAsyncResult = true,
			GetTorProcessesResult = runningTorOsProcesses == 0 ? [] : new[] { new Process() /* Dummy process */ },

			// Cookie file is stored in the profile of that different user, not ours.
			InitTorControlAsyncException = new TorControlException("Cookie file does not exist.")
		};

		await using TorManager torManager = new(settings, processManager);

		NotSupportedException ex = await Assert.ThrowsAsync<NotSupportedException>(
			async () => await torManager.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));
		Assert.Equal(TorManager.TorProcessStartedByDifferentUser, ex.Message);
	}

	class TestProcessManager : TorProcessManager
	{
		public int StartProcessCallCount { get; private set; }
		public List<int> StartedProcessIds { get; } = [];
		public TimeSpan? WaitForTorProcessDelay { get; set; }
		public bool? IsTorRunningAsyncResult { get; set; }
		public bool? EnsureRunningAsyncResult { get; set; }
		public Process[]? GetTorProcessesResult { get; set; }
		public TorControlClient? InitTorControlAsyncResult { get; set; }
		public Exception? InitTorControlAsyncException { get; set; }

		public TestProcessManager(EventBus eventBus)
			: base(eventBus)
		{
		}

		public override Process StartProcess(TorSettings settings, string arguments)
		{
			StartProcessCallCount++;
			Process process = base.StartProcess(settings, arguments);
			StartedProcessIds.Add(process.Id);
			return process;
		}

		public override async Task WaitForProcessExitAsync(Process process, CancellationToken cancellationToken)
		{
			if (WaitForTorProcessDelay is not null)
			{
				await Task.Delay(WaitForTorProcessDelay.Value, cancellationToken);
				return;
			}

			await base.WaitForProcessExitAsync(process, cancellationToken);
		}

		public override async Task<bool> IsTorRunningAsync(EndPoint socksEndpoint, CancellationToken cancellationToken)
		{
			if (IsTorRunningAsyncResult is not null)
			{
				return IsTorRunningAsyncResult.Value;
			}

			return await base.IsTorRunningAsync(socksEndpoint, cancellationToken).ConfigureAwait(false);
		}

		public override async Task<bool> EnsureRunningAsync(Process process, EndPoint socksEndpoint, CancellationToken cancellationToken)
		{
			if (EnsureRunningAsyncResult is not null)
			{
				return EnsureRunningAsyncResult.Value;
			}

			return await base.EnsureRunningAsync(process, socksEndpoint, cancellationToken).ConfigureAwait(false);
		}

		public override Process[] GetTorProcesses()
		{
			if (GetTorProcessesResult is not null)
			{
				return GetTorProcessesResult;
			}

			return base.GetTorProcesses();
		}

		public override async Task<TorControlClient> InitTorControlAsync(TorSettings settings, CancellationToken cancellationToken)
		{
			if (InitTorControlAsyncException is not null)
			{
				throw InitTorControlAsyncException;
			}

			if (InitTorControlAsyncResult is not null)
			{
				return InitTorControlAsyncResult;
			}

			return await base.InitTorControlAsync(settings, cancellationToken).ConfigureAwait(false);
		}
	}
}
