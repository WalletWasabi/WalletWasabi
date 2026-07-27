using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Payjoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Payjoin;
using Xunit;
using OutPoint = NBitcoin.OutPoint;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// The background sweeper's crash-recovery contract: any sender session left open by a
/// dead process resolves to broadcasting its fallback (original) tx and closing,
/// while in-flight sessions and broadcast outages are left alone.
/// </summary>
public class PayjoinSenderManagerTests
{
	private static PayjoinSenderManager CreateManager(PayjoinSenderSessionStore store, List<SmartTransaction> broadcasts) =>
		new(
			store,
			Network.TestNet,
			tx =>
			{
				broadcasts.Add(tx);
				return Task.CompletedTask;
			},
			isTransactionKnown: _ => false);

	private static long CreateAbandonedFfiSession(PayjoinSenderSessionStore store)
	{
		using var pjUri = PayjoinFfiTestHelpers.CreatePjUri();
		string endpoint = pjUri.PjEndpoint();
		Assert.True(Bip77UriParams.TryGetReceiverKey(endpoint, out var receiverKey));

		long sessionId = store.CreateSession(endpoint, receiverKey, "test-wallet").Id;
		using var senderBuilder = new SenderBuilder(PayjoinFfiTestHelpers.OriginalPsbt, pjUri);
		using var initialTransition = senderBuilder.BuildRecommended(1000);
		using var withReplyKey = initialTransition.Save(new SenderSessionPersister(store, sessionId));
		return sessionId;
	}

	[Fact]
	public async Task AbandonedSession_BroadcastsFallbackAndClosesAsync()
	{
		using var store = PayjoinSenderSessionStore.FromFile(":memory:");
		long sessionId = CreateAbandonedFfiSession(store);
		List<SmartTransaction> broadcasts = new();
		using var manager = CreateManager(store, broadcasts);

		await manager.SweepAsync(CancellationToken.None);

		Assert.Single(broadcasts);
		Assert.Empty(store.GetOpenSessions());

		// The ffi event log is terminal too: replay yields a closed session.
		using var replay = PayjoinMethods.ReplaySenderEventLog(new SenderSessionPersister(store, sessionId));
		using var state = replay.State();
		Assert.IsType<SendSession.Closed>(state);
	}

	[Fact]
	public async Task ActiveSession_IsLeftAloneAsync()
	{
		using var store = PayjoinSenderSessionStore.FromFile(":memory:");
		long sessionId = CreateAbandonedFfiSession(store);
		store.MarkActive(sessionId);
		List<SmartTransaction> broadcasts = new();
		using var manager = CreateManager(store, broadcasts);

		await manager.SweepAsync(CancellationToken.None);

		Assert.Empty(broadcasts);
		Assert.Single(store.GetOpenSessions());
	}

	[Fact]
	public async Task UnreplayableSession_BroadcastsStoredFallbackHexAsync()
	{
		using var store = PayjoinSenderSessionStore.FromFile(":memory:");

		var fallbackTx = Network.TestNet.CreateTransaction();
		fallbackTx.Inputs.Add(new OutPoint(uint256.One, 0));
		using Key key = new();
		fallbackTx.Outputs.Add(Money.Coins(0.1m), key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));

		// A session row with a fallback tx but an empty (unreplayable) event log.
		store.CreateSession("https://payjo.in/x#RK1XXX+OH1YYY", "RK1XXX", "test-wallet", fallbackTx.ToHex());

		List<SmartTransaction> broadcasts = new();
		using var manager = CreateManager(store, broadcasts);

		await manager.SweepAsync(CancellationToken.None);

		Assert.Equal(fallbackTx.GetHash(), Assert.Single(broadcasts).Transaction.GetHash());
		Assert.Empty(store.GetOpenSessions());
	}

	[Fact]
	public async Task BroadcastFailure_LeavesSessionOpenForRetryAsync()
	{
		using var store = PayjoinSenderSessionStore.FromFile(":memory:");
		CreateAbandonedFfiSession(store);

		bool broadcasterOnline = false;
		List<SmartTransaction> broadcasts = new();
		using var manager = new PayjoinSenderManager(
			store,
			Network.TestNet,
			tx =>
			{
				if (!broadcasterOnline)
				{
					throw new InvalidOperationException("offline");
				}

				broadcasts.Add(tx);
				return Task.CompletedTask;
			},
			isTransactionKnown: _ => false);

		await manager.SweepAsync(CancellationToken.None);
		Assert.Empty(broadcasts);
		Assert.Single(store.GetOpenSessions()); // Still open, will be retried.

		broadcasterOnline = true;
		await manager.SweepAsync(CancellationToken.None);
		Assert.Single(broadcasts);
		Assert.Empty(store.GetOpenSessions());
	}
}
