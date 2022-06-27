using System.Collections.Generic;
using System.Net.Http;
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
using WalletWasabi.Tor.Control.Utils;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tor;

/// <summary>Monitors Tor process bootstrap and reachability of Wasabi Backend.</summary>
public class TorMonitor : PeriodicRunner
{
	public static readonly TimeSpan CheckIfRunningAfterTorMisbehavedFor = TimeSpan.FromMinutes(5);

	public TorMonitor(TimeSpan period, Uri fallbackBackendUri, TorProcessManager torProcessManager, HttpClientFactory httpClientFactory) : base(period)
	{
		TorProcessManager = torProcessManager;
		TorHttpPool = httpClientFactory.TorHttpPool!;
		HttpClient = httpClientFactory.NewTorHttpClient(Mode.DefaultCircuit);
		TestApiUri = new Uri(fallbackBackendUri, "/api/Software/versions");
	}

	private CancellationTokenSource LoopCts { get; } = new();
	public static bool RequestFallbackAddressUsage { get; private set; }

	/// <summary>Simple Backend API endpoint that allows us to test whether Backend is actually running or not.</summary>
	private Uri TestApiUri { get; }

	/// <summary>When the fallback address was started to be used, <c>null</c> if fallback address is not in use.</summary>
	private DateTime? FallbackStarted { get; set; }

	private TorHttpClient HttpClient { get; }
	private TorProcessManager TorProcessManager { get; }
	private TorHttpPool TorHttpPool { get; }

	private Task? BootstrapTask { get; set; }

	/// <inheritdoc/>
	public override Task StartAsync(CancellationToken cancellationToken)
	{
		BootstrapTask = StartBootstrapMonitorAsync(cancellationToken);
		return base.StartAsync(cancellationToken);
	}

	private async Task StartBootstrapMonitorAsync(CancellationToken appShutdownToken)
	{
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(appShutdownToken, LoopCts.Token);
		while (!linkedCts.IsCancellationRequested)
		{
			try
			{
				(CancellationToken cancellationToken, TorControlClient client) = await TorProcessManager.WaitForNextAttemptAsync(linkedCts.Token).ConfigureAwait(false);
				Logger.LogInfo("Starting Tor bootstrap monitor…");
				List<string> eventNames = new()
				{
					StatusEvent.EventNameStatusGeneral,
					StatusEvent.EventNameStatusClient,
					StatusEvent.EventNameStatusServer,
					CircEvent.EventName,
					OrConnEvent.EventName,
					NetworkLivenessEvent.EventName,
				};
				await client.SubscribeEventsAsync(eventNames, cancellationToken).ConfigureAwait(false);
				bool circuitEstablished = false;
				await foreach (TorControlReply reply in client.ReadEventsAsync(cancellationToken).ConfigureAwait(false))
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
					}
					else if (asyncEvent is CircEvent circEvent)
					{
						CircuitInfo info = circEvent.CircuitInfo;
						if (!circuitEstablished && info.CircStatus is CircStatus.BUILT or CircStatus.EXTENDED or CircStatus.GUARD_WAIT)
						{
							Logger.LogInfo("Tor circuit was established.");
							circuitEstablished = true;
						}
						if (info.CircStatus == CircStatus.CLOSED && info.UserName is not null)
						{
							Logger.LogTrace($"Tor circuit #{info.CircuitID} ('{info.UserName}') was closed.");
							TorHttpPool.ReportCircuitClosed(info.UserName);
						}
					}
				}
			}
			catch (OperationCanceledException)
			{
				if (linkedCts.IsCancellationRequested)
				{
					Logger.LogDebug("Tor Monitor is stopping.");
				}
				else
				{
					Logger.LogDebug("Tor Monitor is re-initializing.");
				}
			}
			catch (Exception e)
			{
				Logger.LogError(e);
				break;
			}
		}
	}

	/// <inheritdoc/>
	protected override async Task ActionAsync(CancellationToken token)
	{
		// If Tor misbehaves and we have not requested the fallback mechanism yet.
		if (!RequestFallbackAddressUsage && TorHttpPool.TorDoesntWorkSince is not null)
		{
			TimeSpan torMisbehavedFor = DateTimeOffset.UtcNow - TorHttpPool.TorDoesntWorkSince ?? TimeSpan.Zero;

			if (torMisbehavedFor > CheckIfRunningAfterTorMisbehavedFor)
			{
				if (TorHttpPool.LatestTorException is TorConnectCommandFailedException)
				{
					// Tor must be running for us to consider switching to the fallback address.
					bool isRunning = await HttpClient.IsTorRunningAsync().ConfigureAwait(false);

					if (!isRunning)
					{
						Logger.LogInfo("Tor is not running.");
						return;
					}

					// Check if the fallback address (clearnet through exit nodes) works. It must work.
					try
					{
						Logger.LogInfo("Tor cannot access remote host. Test fallback URI.");
						using HttpRequestMessage request = new(HttpMethod.Get, TestApiUri);

						// Any HTTP response is fine (e.g. the response message might have the status code 403, 404, etc.) as we test only that
						// the transport layer works.
						using HttpResponseMessage response = await HttpClient.SendAsync(request, token).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						// The fallback address does not work too. We do not want to switch.
						Logger.LogInfo($"Communication through the fallback URL with the backend does not work either. Probably it is down, keep trying... Exception: '{ex}'");
						return;
					}

					Logger.LogInfo("Switching to the fallback URL.");
					RequestFallbackAddressUsage = true;
					FallbackStarted = DateTime.UtcNow;
				}
			}
		}

		// Every two hours try to revert to the original Backend onion Tor address. It might be already fixed.
		// If not, we will get back to the fallback URI again later on.
		if (FallbackStarted is not null && DateTime.UtcNow - FallbackStarted > TimeSpan.FromHours(2))
		{
			Logger.LogInfo("Attempt to revert to the more private onion address of the Backend server.");
			FallbackStarted = null;
			RequestFallbackAddressUsage = false;
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
