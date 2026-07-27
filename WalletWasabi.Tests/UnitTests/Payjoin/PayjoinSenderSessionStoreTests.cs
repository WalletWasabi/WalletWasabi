using System.Linq;
using WalletWasabi.Payjoin;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

public class PayjoinSenderSessionStoreTests
{
	[Fact]
	public void DeduplicatesOnEndpointAndReceiverKeyIncludingCompleted()
	{
		using var store = PayjoinSenderSessionStore.FromFile(":memory:");

		var record = store.CreateSession("https://payjo.in/a#RK1AAA+OH1BBB", "RK1AAA", "wallet");

		// Same endpoint, open session.
		var exOpen = Assert.Throws<PayjoinDuplicateSessionException>(
			() => store.CreateSession("https://payjo.in/a#RK1AAA+OH1BBB", "RK1OTHER", "wallet"));
		Assert.False(exOpen.Existing.IsCompleted);

		// Same receiver key, different endpoint.
		Assert.Throws<PayjoinDuplicateSessionException>(
			() => store.CreateSession("https://other.example/b#RK1AAA+OH1BBB", "RK1AAA", "wallet"));

		// Dedup persists after completion (address/HPKE-key reuse prevention).
		store.CompleteSession(record.Id);
		var exCompleted = Assert.Throws<PayjoinDuplicateSessionException>(
			() => store.CreateSession("https://payjo.in/a#RK1AAA+OH1BBB", "RK1AAA", "wallet"));
		Assert.True(exCompleted.Existing.IsCompleted);

		// A genuinely new session is fine.
		store.CreateSession("https://payjo.in/c#RK1CCC+OH1BBB", "RK1CCC", "wallet");
	}

	[Fact]
	public void EventLogIsOrderedAndCompletionIsIdempotent()
	{
		using var store = PayjoinSenderSessionStore.FromFile(":memory:");

		var first = store.CreateSession("https://payjo.in/a#RK1AAA", "RK1AAA", "wallet-1");
		var second = store.CreateSession("https://payjo.in/b#RK1BBB", "RK1BBB", "wallet-2");

		store.AppendEvent(first.Id, "e1");
		store.AppendEvent(second.Id, "other");
		store.AppendEvent(first.Id, "e2");
		store.AppendEvent(first.Id, "e3");

		Assert.Equal(new[] { "e1", "e2", "e3" }, store.LoadEvents(first.Id));

		Assert.Equal(2, store.GetOpenSessions().Count);
		store.CompleteSession(first.Id);
		store.CompleteSession(first.Id); // Idempotent.
		Assert.Equal(second.Id, Assert.Single(store.GetOpenSessions()).Id);

		Assert.True(store.TryFindSession(first.Endpoint, first.ReceiverKey, out var found));
		Assert.True(found.IsCompleted);

		store.SetFallbackTx(second.Id, "beef");
		Assert.Equal("beef", store.GetOpenSessions().Single().FallbackTxHex);
	}

	[Fact]
	public void ActiveSessionGuard()
	{
		using var store = PayjoinSenderSessionStore.FromFile(":memory:");
		var record = store.CreateSession("https://payjo.in/a#RK1AAA", "RK1AAA", "wallet");

		Assert.False(store.IsActive(record.Id));
		store.MarkActive(record.Id);
		Assert.True(store.IsActive(record.Id));
		store.UnmarkActive(record.Id);
		Assert.False(store.IsActive(record.Id));
	}
}
