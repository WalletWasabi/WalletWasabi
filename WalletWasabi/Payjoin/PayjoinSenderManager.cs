using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using PjFfi = global::Payjoin;

namespace WalletWasabi.Payjoin;

/// <summary>
/// Background sweeper for BIP 77 sender sessions that were left open — the app died
/// mid-negotiation before the send flow could broadcast anything. Policy:
/// the user asked to pay, so complete the payment. A late proposal cannot be signed here
/// (no wallet password in the background), therefore every abandoned session resolves to
/// broadcasting its fallback (original) tx and closing, except when the payjoin tx itself
/// is already known to the wallet. Broadcast failures leave the session open for the next
/// tick; sessions older than <see cref="GiveUpAfter"/> are closed loudly.
/// </summary>
public class PayjoinSenderManager : PeriodicRunner
{
	private static readonly TimeSpan SweepPeriod = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(30);

	private readonly Network _network;
	private readonly Func<SmartTransaction, Task> _broadcastAsync;
	private readonly Func<uint256, bool> _isTransactionKnown;
	private int _recoveryBroadcastCount;

	public PayjoinSenderManager(
		PayjoinSenderSessionStore sessionStore,
		Network network,
		Func<SmartTransaction, Task> broadcastAsync,
		Func<uint256, bool> isTransactionKnown)
		: base(SweepPeriod)
	{
		SessionStore = sessionStore;
		_network = network;
		_broadcastAsync = broadcastAsync;
		_isTransactionKnown = isTransactionKnown;
	}

	public PayjoinSenderSessionStore SessionStore { get; }

	protected override async Task ActionAsync(CancellationToken cancel) => await SweepAsync(cancel).ConfigureAwait(false);

	internal async Task SweepAsync(CancellationToken cancel)
	{
		foreach (var session in SessionStore.GetOpenSessions())
		{
			cancel.ThrowIfCancellationRequested();

			// Sessions currently driven by the send flow are not abandoned.
			if (SessionStore.IsActive(session.Id))
			{
				continue;
			}

			try
			{
				await ResolveAbandonedSessionAsync(session, cancel).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex) when (session.CreatedAt < DateTimeOffset.UtcNow - GiveUpAfter)
			{
				Logger.LogError($"Giving up on payjoin session {session.Id} after {GiveUpAfter.TotalDays} days: {ex.Message} The fallback tx was NOT confirmed broadcast; hex: {session.FallbackTxHex}");
				SessionStore.CompleteSession(session.Id);
			}
			catch (Exception ex)
			{
				// Typically the broadcaster being offline; the session stays open for the next tick.
				Logger.LogWarning($"Could not resolve abandoned payjoin session {session.Id} yet: {ex.Message}");
			}
		}
	}

	private async Task ResolveAbandonedSessionAsync(PayjoinSenderSessionRecord session, CancellationToken cancel)
	{
		var persister = new SenderSessionPersister(SessionStore, session.Id);

		PjFfi.SenderReplayResult replay;
		try
		{
			replay = PjFfi.PayjoinMethods.ReplaySenderEventLog(persister);
		}
		catch (Exception ex)
		{
			// Expired or unreadable event log; fall back to the tx hex stored at creation.
			Logger.LogWarning($"Payjoin session {session.Id} could not be replayed ({ex.Message}); resolving from the stored fallback tx.");
			await BroadcastStoredFallbackAsync(session, cancel).ConfigureAwait(false);
			SessionStore.CompleteSession(session.Id);
			return;
		}

		using var replayDisposal = replay;
		using var state = replay.State();
		switch (state)
		{
			case PjFfi.SendSession.Closed closed when closed.Inner.IsSuccess():
			{
				string? proposalPsbt = closed.Inner.SuccessPsbtBase64();
				if (proposalPsbt is { } psbt && _isTransactionKnown(PSBT.Parse(psbt, _network).GetGlobalTransaction().GetHash()))
				{
					// The payjoin tx made it out before the crash; nothing to do.
					SessionStore.CompleteSession(session.Id);
					return;
				}

				// A proposal arrived but was never signed/broadcast, and it cannot be
				// signed here — the payment still completes via the fallback tx.
				await BroadcastStoredFallbackAsync(session, cancel).ConfigureAwait(false);
				SessionStore.CompleteSession(session.Id);
				return;
			}

			case PjFfi.SendSession.Closed:
				// Aborted; there is nothing this side still owes.
				SessionStore.CompleteSession(session.Id);
				return;

			case PjFfi.SendSession.SenderPendingFallback pendingFallback:
				await BroadcastAndCloseAsync(pendingFallback.Inner, persister, session.Id, cancel).ConfigureAwait(false);
				return;

			case PjFfi.SendSession.PollingForProposal polling:
				await CancelBroadcastCloseAsync(polling.Inner.Cancel(), persister, session.Id, cancel).ConfigureAwait(false);
				return;

			case PjFfi.SendSession.WithReplyKey withReplyKey:
				await CancelBroadcastCloseAsync(withReplyKey.Inner.Cancel(), persister, session.Id, cancel).ConfigureAwait(false);
				return;

			default:
				Logger.LogWarning($"Payjoin session {session.Id} replayed to an unexpected state; leaving it open.");
				return;
		}
	}

	private async Task CancelBroadcastCloseAsync(PjFfi.SenderCancelTransition cancelTransition, SenderSessionPersister persister, long sessionId, CancellationToken cancel)
	{
		using var cancelDisposal = cancelTransition;
		using var pendingFallback = cancelTransition.Save(persister);
		await BroadcastAndCloseAsync(pendingFallback, persister, sessionId, cancel).ConfigureAwait(false);
	}

	private async Task BroadcastAndCloseAsync(PjFfi.SenderPendingFallback pendingFallback, SenderSessionPersister persister, long sessionId, CancellationToken cancel)
	{
		// Broadcast before closing: if the broadcast fails, the persisted state stays
		// PendingFallback and the next tick retries.
		await BroadcastAsync(Transaction.Load(pendingFallback.FallbackTx(), _network), cancel).ConfigureAwait(false);
		using var broadcastedTransition = pendingFallback.Close();
		broadcastedTransition.Save(persister);
		SessionStore.CompleteSession(sessionId);
	}

	public override void Dispose()
	{
		SessionStore.Dispose();
		base.Dispose();
	}

	private async Task BroadcastStoredFallbackAsync(PayjoinSenderSessionRecord session, CancellationToken cancel)
	{
		if (session.FallbackTxHex is not { } fallbackTxHex)
		{
			Logger.LogWarning($"Payjoin session {session.Id} has no stored fallback tx; closing without broadcast.");
			return;
		}

		await BroadcastAsync(Transaction.Parse(fallbackTxHex, _network), cancel).ConfigureAwait(false);
	}

	private async Task BroadcastAsync(Transaction fallbackTx, CancellationToken cancel)
	{
		cancel.ThrowIfCancellationRequested();

		// Recovery metric: frequent crash-recovery broadcasts are the signal for
		// prioritizing true pending-async sends.
		int count = Interlocked.Increment(ref _recoveryBroadcastCount);
		Logger.LogInfo($"Broadcasting payjoin fallback tx {fallbackTx.GetHash()} (crash-recovery broadcast #{count} this run).");

		await _broadcastAsync(new SmartTransaction(fallbackTx)).ConfigureAwait(false);
	}
}
