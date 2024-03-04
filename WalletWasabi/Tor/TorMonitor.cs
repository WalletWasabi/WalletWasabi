using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Tor.Control.Messages.CircuitStatus;
using WalletWasabi.Tor.Control.Messages.Events;
using WalletWasabi.Tor.Control.Messages.Events.OrEvents;
using WalletWasabi.Tor.Control.Messages.Events.StatusEvents;
using WalletWasabi.Tor.Control.Messages.StreamStatus;
using WalletWasabi.Tor.Control.Utils;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tor;

/// <summary>Monitors Tor process bootstrap and reachability of Wasabi Backend.</summary>
public class TorMonitor : PeriodicRunner
{
	public static readonly TimeSpan CheckIfRunningAfterTorMisbehavedFor = TimeSpan.FromMinutes(10);

	/// <summary>Tor Control events we monitor.</summary>
	private static readonly IReadOnlyList<string> EventNames = new List<string>()
	{
		StatusEvent.EventNameStatusGeneral,
		StatusEvent.EventNameStatusClient,
		StatusEvent.EventNameStatusServer,
		CircEvent.EventName,
		StreamEvent.EventName,
		OrConnEvent.EventName,
		NetworkLivenessEvent.EventName,
	};

	/// <remarks>Guards <see cref="ForceTorRestartCts"/>.</remarks>
	private readonly object _lock = new();

	public TorMonitor(TimeSpan period, TorProcessManager torProcessManager, WasabiHttpClientFactory httpClientFactory) : base(period)
	{
		TorProcessManager = torProcessManager;
		TorHttpPool = httpClientFactory.TorHttpPool!;
		HttpClient = httpClientFactory.NewTorHttpClient(Mode.DefaultCircuit);
	}

	private CancellationTokenSource LoopCts { get; } = new();

	private TorHttpClient HttpClient { get; }
	private TorProcessManager TorProcessManager { get; }
	private TorHttpPool TorHttpPool { get; }

	private Task? BootstrapTask { get; set; }

	/// <summary>Whether we should try Tor process restart to fix <see cref="ReplyType.TtlExpired"/> issues.</summary>
	private bool TryTorRestart { get; set; } = true;

	/// <remarks>Assignment and cancel operations must be guarded with <see cref="_lock"/>.</remarks>
	private CancellationTokenSource? ForceTorRestartCts { get; set; }

	/// <inheritdoc/>
	public override Task StartAsync(CancellationToken cancellationToken)
	{
		BootstrapTask = StartBootstrapMonitorAsync(cancellationToken);
		return base.StartAsync(cancellationToken);
	}

	private async Task StartBootstrapMonitorAsync(CancellationToken appShutdownToken)
	{
		try
		{
			using CancellationTokenSource linkedLoopCts = CancellationTokenSource.CreateLinkedTokenSource(appShutdownToken, LoopCts.Token);

			while (!linkedLoopCts.IsCancellationRequested)
			{
				await MonitorEventsAsync(linkedLoopCts.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogDebug("Application is shutting down.");
		}
	}

	/// <remarks>Method is invoked every time Tor process is started.</remarks>
	private async Task MonitorEventsAsync(CancellationToken cancellationToken)
	{
		using CancellationTokenSource forceTorRestartCts = new();
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(forceTorRestartCts.Token, cancellationToken);

		lock (_lock)
		{
			ForceTorRestartCts = forceTorRestartCts;
		}

		TorControlClient? torControlClient = null;

		try
		{
			// We can't use linked CTS here because then we would not obtain Tor control client instance to actually shut down Tor if force restart is signaled.
			(CancellationToken torTerminatedCancellationToken, torControlClient) = await TorProcessManager.WaitForNextAttemptAsync(cancellationToken).ConfigureAwait(false);
			using CancellationTokenSource linkedCts2 = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token, torTerminatedCancellationToken);

			Logger.LogInfo("Starting Tor bootstrap monitor…");
			await torControlClient.SubscribeEventsAsync(EventNames, linkedCts2.Token).ConfigureAwait(false);
			bool circuitEstablished = false;

			await foreach (TorControlReply reply in torControlClient.ReadEventsAsync(linkedCts2.Token).ConfigureAwait(false))
			{
				IAsyncEvent asyncEvent;
				try
				{
					asyncEvent = AsyncEventParser.Parse(reply);
				}
				catch (TorControlReplyParseException e)
				{
					Logger.LogError($"Exception thrown when parsing event: '{reply}'", e);
					continue;
				}
				if (asyncEvent is BootstrapStatusEvent bootstrapEvent)
				{
					BootstrapStatusEvent.Phases.TryGetValue(bootstrapEvent.Progress, out string? bootstrapInfo);
					Logger.LogInfo($"Bootstrap progress: {bootstrapEvent.Progress}/100 ({bootstrapInfo ?? "N/A"})");
				}
				else if (asyncEvent is StatusEvent statusEvent)
				{
					if (!circuitEstablished && statusEvent.Action == StatusEvent.ActionCircuitEstablished)
					{
						Logger.LogInfo("Tor circuit was established.");
						circuitEstablished = true;
					}

					if (statusEvent.Action == StatusEvent.ActionCircuitNotEstablished)
					{
						statusEvent.Arguments.TryGetValue("REASON", out string? reason);
						Logger.LogError($"Tor circuit failed to be established: {(reason ?? "unknown reason")}.");
					}
				}
				else if (asyncEvent is CircEvent circEvent)
				{
					CircuitInfo info = circEvent.CircuitInfo;

					if (!circuitEstablished && (info.CircuitStatus is CircuitStatus.BUILT or CircuitStatus.EXTENDED or CircuitStatus.GUARD_WAIT))
					{
						Logger.LogInfo("Tor circuit was established.");
						circuitEstablished = true;
					}
				}
				else if (asyncEvent is StreamEvent streamEvent)
				{
					StreamInfo info = streamEvent.StreamInfo;

					if (info.UserName is not null)
					{
						TorHttpPool.ReportStreamStatus(streamUsername: info.UserName, streamStatus: info.StreamStatus, circuitID: info.CircuitID);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			if (forceTorRestartCts.IsCancellationRequested)
			{
				// Attempt to shut down Tor.
				if (torControlClient is not null)
				{
					bool success = await ShutDownTorAsync(torControlClient).ConfigureAwait(false);
					if (success)
					{
						// Wait a bit so that we don't try to connect to Tor Control until Tor is actually started.
						await Task.Delay(2_000, cancellationToken).ConfigureAwait(false);
					}
				}
				else
				{
					Logger.LogWarning("No Tor control client to restart Tor."); // This should not happen.
				}
			}
			else if (cancellationToken.IsCancellationRequested)
			{
				Logger.LogDebug("Tor Monitor is stopping.");
			}
			else
			{
				Logger.LogDebug("Tor Monitor is re-initializing.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
		finally
		{
			lock (_lock)
			{
				ForceTorRestartCts = null;
			}
		}
	}

	private async Task<bool> ShutDownTorAsync(TorControlClient torControlClient)
	{
		try
		{
			// The shutdown operation should be quick but let's not risk getting stuck for some reason.
			using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
			TorControlReply reply = await torControlClient.SignalShutdownAsync(timeoutCts.Token).ConfigureAwait(false);

			if (reply.Success)
			{
				Logger.LogInfo("Tor process was gracefully shut down to be restarted again.");
				return true;
			}
			else
			{
				Logger.LogInfo("Failed to restart Tor process.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogDebug(ex);
		}

		return false;
	}

	/// <inheritdoc/>
	protected override async Task ActionAsync(CancellationToken token)
	{
		// If Tor misbehaves and we have not requested the fallback mechanism yet.
		if (TorHttpPool.TorDoesntWorkSince is not null)
		{
			TimeSpan torMisbehavedFor = DateTimeOffset.UtcNow - TorHttpPool.TorDoesntWorkSince ?? TimeSpan.Zero;

			if (torMisbehavedFor > CheckIfRunningAfterTorMisbehavedFor)
			{
				Exception? latestTorException = TorHttpPool.LatestTorException;

				if (latestTorException is null)
				{
					return;
				}

				if (latestTorException is TorConnectCommandFailedException e)
				{
					bool isRunning = await HttpClient.IsTorRunningAsync(token).ConfigureAwait(false);

					if (!isRunning)
					{
						Logger.LogInfo("Tor is not running.");
						return;
					}

					if (TryTorRestart && e.RepField == RepField.TtlExpired)
					{
						Logger.LogDebug("Request Tor restart to fix TTL issues.");

						// This might be a no-op in case of Tor being started. We don't mind this behavior.
						lock (_lock)
						{
							if (ForceTorRestartCts is not null)
							{
								TryTorRestart = false;
								ForceTorRestartCts.Cancel(); // Dispose is handled in a different method.
								ForceTorRestartCts = null;
							}
						}

						return;
					}

					TryTorRestart = true;
				}
				else if (torMisbehavedFor - CheckIfRunningAfterTorMisbehavedFor < Period)
				{
					Logger.LogDebug("Latest Tor exception is.", latestTorException);
				}
			}
		}
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		LoopCts.Cancel();
		if (BootstrapTask is not null)
		{
			Logger.LogDebug("Wait until Tor bootstrap monitor finishes.");
			await BootstrapTask.ConfigureAwait(false);
		}
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}
}
