using NBitcoin;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Payjoin;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

public class PayjoinSessionStoreTests
{
	/// <summary>
	/// The inputs-seen store is the probing-attack defense: an outpoint offered in one
	/// original proposal must be rejected in every later session, including after restart.
	/// </summary>
	[Fact]
	public async Task InputSeen_SecondInsertIsRejected_AcrossRestarts()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		string dbPath = Path.Combine(workDir, "Sessions.sqlite");
		var outpoint = new OutPoint(uint256.One, 3);

		using (var store = PayjoinSessionStore.FromFile(dbPath))
		{
			Assert.True(store.TryInsertInputSeen(outpoint));
			Assert.False(store.TryInsertInputSeen(outpoint));
			Assert.True(store.TryInsertInputSeen(new OutPoint(uint256.One, 4)));
		}

		using (var store = PayjoinSessionStore.FromFile(dbPath))
		{
			Assert.False(store.TryInsertInputSeen(outpoint));
		}
	}

	[Fact]
	public async Task EventLog_IsAppendOnly_AndPerSession()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		using var store = PayjoinSessionStore.FromFile(Path.Combine(workDir, "Sessions.sqlite"));

		string first = store.CreateSession("wallet-a", "addr-a");
		string second = store.CreateSession("wallet-b", "addr-b");

		store.AppendEvent(first, "event-1");
		store.AppendEvent(second, "other-1");
		store.AppendEvent(first, "event-2");

		Assert.Equal(new[] { "event-1", "event-2" }, store.LoadEvents(first));
		Assert.Equal(new[] { "other-1" }, store.LoadEvents(second));
	}

	[Fact]
	public async Task CloseSession_RemovesFromActiveSessions()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		using var store = PayjoinSessionStore.FromFile(Path.Combine(workDir, "Sessions.sqlite"));

		string kept = store.CreateSession("wallet", "addr-kept");
		string closed = store.CreateSession("wallet", "addr-closed");

		store.CloseSession(closed);

		var active = store.GetActiveSessions();
		Assert.Equal(kept, Assert.Single(active).Id);

		var closedRecord = store.TryGetSession(closed);
		Assert.NotNull(closedRecord);
		Assert.NotNull(closedRecord.CompletedAt);
	}

	[Fact]
	public async Task SessionMetadata_RoundTrips()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		using var store = PayjoinSessionStore.FromFile(Path.Combine(workDir, "Sessions.sqlite"));

		string id = store.CreateSession("wallet", "addr");
		var outpoint = new OutPoint(new uint256(7), 1);
		var txid = new uint256(42);

		store.SetPjUri(id, "bitcoin:addr?pj=https://example.com");
		store.SetReservedOutpoint(id, outpoint);
		store.SetProposalTxid(id, txid);

		var record = store.TryGetSession(id);
		Assert.NotNull(record);
		Assert.Equal("bitcoin:addr?pj=https://example.com", record.PjUri);
		Assert.Equal(outpoint, record.ReservedOutpoint);
		Assert.Equal(txid, record.ProposalTxid);
		Assert.Null(record.CompletedAt);
	}
}
