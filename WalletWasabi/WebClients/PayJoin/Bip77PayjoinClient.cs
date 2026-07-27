using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Logging;
using WalletWasabi.Payjoin;
using PjFfi = global::Payjoin;

namespace WalletWasabi.WebClients.PayJoin;

/// <summary>
/// BIP 77 (async payjoin, v2) sender driven by payjoin-ffi's typestate machine:
/// SenderBuilder → WithReplyKey (OHTTP POST of the original proposal) → PollingForProposal
/// (directory long-poll, no client-side sleep) → proposal PSBT. The ffi is a pure state
/// machine — all bytes flow through Wasabi's HttpClient factory (Tor stream isolation per
/// session/relay name). Every transition is persisted to <see cref="PayjoinSenderSessionStore"/>
/// before it is acted on, so a killed session replays on startup.
/// Every failure surfaces as <see cref="PayjoinException"/> (with <see cref="DowngradeReason"/>
/// set) so the caller's existing fallback path broadcasts the original transaction —
/// which is exactly the BIP 77 fallback tx. The session is cancelled and closed first,
/// so a later proposal can never compete with the broadcast original.
/// </summary>
public class Bip77PayjoinClient : IPayjoinClient
{
	private readonly string _bip21Uri;
	private readonly PayjoinSenderSessionStore _store;
	private readonly Func<string, HttpClient> _httpClientFactory;
	private readonly string _walletName;
	private readonly Network _network;
	private readonly TimeSpan _pollWindow;
	private readonly List<string> _relays;
	private readonly HashSet<string> _failedRelays = new();

	public Bip77PayjoinClient(
		string bip21Uri,
		string pjEndpoint,
		PayjoinSenderSessionStore store,
		Func<string, HttpClient> httpClientFactory,
		string walletName,
		Network network,
		IEnumerable<string>? ohttpRelays = null,
		TimeSpan? pollWindow = null)
	{
		_bip21Uri = bip21Uri;
		PaymentUrl = new Uri(pjEndpoint);
		PjEndpoint = pjEndpoint;
		_store = store;
		_httpClientFactory = httpClientFactory;
		_walletName = walletName;
		_network = network;
		_pollWindow = pollWindow ?? PayjoinConstants.DefaultPollWindow;
		_relays = (ohttpRelays ?? PayjoinConstants.DefaultOhttpRelays).OrderBy(_ => Random.Shared.Next()).ToList();
	}

	/// <summary>The BIP 77 directory endpoint (fragment params included).</summary>
	public Uri PaymentUrl { get; }

	public string PjEndpoint { get; }

	/// <summary>
	/// Set when the payjoin attempt degraded to a plain send; plain-language, shown to the
	/// user by the send flow after the fallback broadcast.
	/// </summary>
	public string? DowngradeReason { get; private set; }

	public async Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey accountKey, RootedKeyPath rootedKeyPath, HdPubKey changeHdPubKey, CancellationToken cancellationToken)
	{
		if (originalTx.IsAllFinalized())
		{
			throw new InvalidOperationException("The original PSBT should not be finalized.");
		}

		if (!Bip77UriParams.TryGetReceiverKey(PjEndpoint, out string? receiverKey))
		{
			throw Downgrade("The payjoin link is missing its receiver parameters.");
		}

		// Same strip-down the BIP 78 client performs: send a finalized original that carries
		// no key path or xpub information.
		var cloned = originalTx.Clone();
		if (!cloned.TryFinalize(out _))
		{
			throw new InvalidOperationException("The original PSBT could not be finalized.");
		}

		foreach (var finalized in cloned.Inputs.Where(i => i.IsFinalized()))
		{
			finalized.ClearForFinalize();
		}

		foreach (var output in cloned.Outputs)
		{
			output.HDKeyPaths.Clear();
		}

		cloned.GlobalXPubs.Clear();

		// The finalized original doubles as the BIP 77 fallback tx; store it for recovery.
		Transaction fallbackTx = cloned.ExtractTransaction();

		PayjoinSenderSessionRecord session;
		try
		{
			session = _store.CreateSession(PjEndpoint, receiverKey, _walletName, fallbackTx.ToHex());
		}
		catch (PayjoinDuplicateSessionException ex)
		{
			DowngradeReason = ex.Message;
			throw;
		}

		_store.MarkActive(session.Id);
		try
		{
			var persister = new SenderSessionPersister(_store, session.Id);
			return await RunSenderSessionAsync(cloned.ToBase64(), originalTx, persister, session.Id, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_store.UnmarkActive(session.Id);
		}
	}

	private async Task<PSBT> RunSenderSessionAsync(string finalizedPsbtBase64, PSBT originalTx, SenderSessionPersister persister, long sessionId, CancellationToken cancellationToken)
	{
		using CancellationTokenSource windowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		windowCts.CancelAfter(_pollWindow);

		PjFfi.PollingForProposal? polling = null;
		try
		{
			using var ffiUri = PjFfi.Uri.Parse(_bip21Uri);
			using var pjUri = ffiUri.CheckPjSupported();
			using var senderBuilder = new PjFfi.SenderBuilder(finalizedPsbtBase64, pjUri);
			using var initialTransition = senderBuilder.BuildRecommended(GetMinFeeRateSatPerKwu(originalTx));
			using var withReplyKey = initialTransition.Save(persister);

			polling = await PostOriginalProposalAsync(withReplyKey, persister, windowCts.Token).ConfigureAwait(false);

			while (true)
			{
				windowCts.Token.ThrowIfCancellationRequested();
				var (proposal, next) = await PollOnceAsync(polling, persister, windowCts.Token).ConfigureAwait(false);
				if (proposal is { } psbtBase64)
				{
					// The event log is terminal (Closed/Success) at this point.
					_store.CompleteSession(sessionId);
					return PSBT.Parse(psbtBase64, _network);
				}

				polling.Dispose();
				polling = next!;
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// The in-dialog poll window elapsed without a proposal. Cancel and close the
			// session so the degrade path can safely broadcast the original (= fallback) tx.
			CancelAndCloseSession(polling, persister, sessionId);
			throw Downgrade("The receiver did not respond in time, so the payment was sent as a normal transaction.");
		}
		catch (OperationCanceledException)
		{
			CancelAndCloseSession(polling, persister, sessionId);
			throw;
		}
		catch (PayjoinException ex)
		{
			CancelAndCloseSession(polling, persister, sessionId);
			DowngradeReason ??= ex.Message;
			throw;
		}
		catch (Exception ex)
		{
			// payjoin-ffi (uniffi) or unexpected failure: never leak past the payjoin seam.
			Logger.LogWarning($"BIP 77 payjoin failed: {ex}");
			CancelAndCloseSession(polling, persister, sessionId);
			throw Downgrade(FriendlyFfiMessage(ex));
		}
		finally
		{
			polling?.Dispose();
		}
	}

	private async Task<PjFfi.PollingForProposal> PostOriginalProposalAsync(PjFfi.WithReplyKey withReplyKey, SenderSessionPersister persister, CancellationToken cancellationToken)
	{
		foreach (string relay in AvailableRelays())
		{
			cancellationToken.ThrowIfCancellationRequested();

			// A fresh request per relay is a fresh OHTTP encapsulation; only retransmitting
			// identical ciphertext is forbidden.
			using var requestContext = withReplyKey.CreateV2PostRequest(relay);
			byte[] responseBody;
			try
			{
				responseBody = await PostAsync(relay, requestContext.Request, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex)
			{
				QuarantineRelay(relay, ex);
				continue;
			}

			using var transition = withReplyKey.ProcessResponse(responseBody, requestContext.OhttpCtx);
			return transition.Save(persister);
		}

		throw new PayjoinException("None of the payjoin relays could be reached.");
	}

	private async Task<(string? ProposalPsbt, PjFfi.PollingForProposal? Next)> PollOnceAsync(PjFfi.PollingForProposal polling, SenderSessionPersister persister, CancellationToken cancellationToken)
	{
		foreach (string relay in AvailableRelays())
		{
			cancellationToken.ThrowIfCancellationRequested();

			using var requestContext = polling.CreatePollRequest(relay);
			byte[] responseBody;
			try
			{
				responseBody = await PostAsync(relay, requestContext.Request, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException ex)
			{
				QuarantineRelay(relay, ex);
				continue;
			}

			using var transition = polling.ProcessResponse(responseBody, requestContext.OhttpCtx);
			var outcome = transition.Save(persister);
			return outcome switch
			{
				PjFfi.PollingForProposalTransitionOutcome.Progress progress => (progress.PsbtBase64, null),

				// Do not dispose a Stasis outcome: its Inner is the next polling state.
				PjFfi.PollingForProposalTransitionOutcome.Stasis stasis => (null, stasis.Inner),
				_ => throw new PayjoinException("Unexpected payjoin polling outcome."),
			};
		}

		throw new PayjoinException("None of the payjoin relays could be reached.");
	}

	private async Task<byte[]> PostAsync(string relay, PjFfi.Request request, CancellationToken cancellationToken)
	{
		// One client name per session/relay pair keeps Tor stream isolation intact.
		HttpClient httpClient = _httpClientFactory($"payjoin-{PjEndpoint.GetHashCode():x8}-{new Uri(relay).Host}");

		using var requestMessage = new HttpRequestMessage(HttpMethod.Post, request.Url);
		using var content = new ByteArrayContent(request.Body);
		content.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);
		requestMessage.Content = content;

		using HttpResponseMessage response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException($"Payjoin relay '{relay}' returned status code {(int)response.StatusCode}.");
		}

		return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
	}

	private IEnumerable<string> AvailableRelays() => _relays.Where(r => !_failedRelays.Contains(r));

	private void QuarantineRelay(string relay, Exception ex)
	{
		Logger.LogWarning($"Payjoin relay '{relay}' failed, trying the next one. {ex.Message}");
		_failedRelays.Add(relay);
	}

	/// <summary>
	/// Typed cancel on whatever state the session reached, then close: the fallback tx's
	/// control is transferred to the caller's degrade path, which broadcasts it.
	/// </summary>
	private void CancelAndCloseSession(PjFfi.PollingForProposal? polling, SenderSessionPersister persister, long sessionId)
	{
		try
		{
			if (polling is { } state)
			{
				using var cancelTransition = state.Cancel();
				using var pendingFallback = cancelTransition.Save(persister);
				using var broadcastedTransition = pendingFallback.Close();
				broadcastedTransition.Save(persister);
			}
		}
		catch (Exception ex)
		{
			// Best effort: the startup sweeper resolves sessions this failed to close.
			Logger.LogWarning($"Failed to close payjoin session cleanly: {ex.Message}");
		}
		finally
		{
			_store.CompleteSession(sessionId);
		}
	}

	private PayjoinException Downgrade(string reason)
	{
		DowngradeReason = reason;
		return new PayjoinException(reason);
	}

	internal static string FriendlyFfiMessage(Exception ex) =>
		ex switch
		{
			PjFfi.ResponseException.WellKnown wellKnown => wellKnown.v1.Code() switch
			{
				PjFfi.ErrorCode.Unavailable => "The receiver's payjoin service is currently unavailable, so the payment was sent as a normal transaction.",
				PjFfi.ErrorCode.NotEnoughMoney => "The receiver could not add funds to the payjoin, so the payment was sent as a normal transaction.",
				PjFfi.ErrorCode.VersionUnsupported => "The receiver does not support this payjoin version, so the payment was sent as a normal transaction.",
				PjFfi.ErrorCode.OriginalPsbtRejected => "The receiver rejected the payment proposal, so the payment was sent as a normal transaction.",
				_ => "The receiver reported a payjoin error, so the payment was sent as a normal transaction.",
			},
			PjFfi.CreateRequestException createRequest when createRequest.IsExpired() => "The payjoin link has expired, so the payment was sent as a normal transaction.",
			PjFfi.UriParseException or PjFfi.PjNotSupported => "The payjoin link could not be understood, so the payment was sent as a normal transaction.",
			_ => "Payjoin failed, so the payment was sent as a normal transaction.",
		};

	private static ulong GetMinFeeRateSatPerKwu(PSBT originalTx)
	{
		// 1 vbyte = 4 weight units, so sat/kwu = (sat/kvb) / 4. Floor at the 1 sat/vb
		// broadcast minimum (250 sat/kwu).
		if (originalTx.TryGetEstimatedFeeRate(out FeeRate? feeRate))
		{
			return Math.Max(250UL, (ulong)(feeRate.FeePerK.Satoshi / 4));
		}

		return 250UL;
	}
}
