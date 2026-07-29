using System.IO;
using System.Threading.Tasks;
using NBitcoin;
using Payjoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Payjoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Userfacing;
using WalletWasabi.WebClients.PayJoin;
using Xunit;
using Uri = System.Uri;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// Tests for the BIP 77 (async payjoin) integration.
/// Skipped tests are stubs for behavior that lands with the payjoin-ffi-driven sender/receiver;
/// active tests pin down current behavior the integration builds on.
/// </summary>
public class Bip77PayjoinTests
{
	/// <summary>
	/// A realistic BIP 77 <c>pj=</c> value: a directory URL whose fragment carries the receiver's
	/// ephemeral parameters as uppercase bech32, '+'-delimited (<c>EX1…</c> expiry, <c>OH1…</c>
	/// OHTTP keys, <c>RK1…</c> receiver key) — the shape rust-payjoin emits (core/uri/v2.rs).
	/// </summary>
	private const string Bip77PjUrl =
		"https://payjo.in/AbCd1234#EX1C4UC6ES+OH1QYPM5JXYNS754Y4R45QWE336QFYGGBW02VQHPARPAR4MBM8FMCLJPRE+RK1Q0DJS3VVDXWQQTLQ8022QGXSX7ML9PHZ6EDSF6AKEWQG758JPS2EV";

	/// <summary>
	/// Inside a BIP 21 URI the whole pj value is percent-encoded; <see cref="AddressParser"/> must
	/// hand back the decoded URL, fragment intact, so the sender can feed it to payjoin-ffi.
	/// This pins the current parser behavior (HttpUtility.ParseQueryString percent-decodes).
	/// </summary>
	[Fact]
	public void AddressParser_PreservesBip77PjUrlFragmentParameters()
	{
		string encodedPjUrl = Uri.EscapeDataString(Bip77PjUrl);
		string bip21 = $"bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=0.00010727&pj={encodedPjUrl}";

		var result = AddressParser.Parse(bip21, Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Equal(Bip77PjUrl, uri.PayjoinEndpoint);
		Assert.Equal(0.00010727m, uri.Amount);
	}

	/// <summary>
	/// <c>pjos=0</c> (output substitution disabled) must survive parsing so the sender can
	/// rebuild a faithful BIP 21 for payjoin-ffi.
	/// </summary>
	[Fact]
	public void AddressParser_PreservesPjosParameter()
	{
		string bip21 = $"bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=1&pj={Uri.EscapeDataString(Bip77PjUrl)}&pjos=0";

		var result = AddressParser.Parse(bip21, Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Equal("0", uri.PayjoinOutputSubstitution);

		// And it round-trips through serialization.
		Assert.Contains("pjos=0", uri.ToWif(Network.TestNet));
	}

	[Fact]
	public void Bip77UriParams_DetectsVersionAndExtractsReceiverKey()
	{
		Assert.True(Bip77UriParams.IsBip77(Bip77PjUrl));
		Assert.True(Bip77UriParams.TryGetReceiverKey(Bip77PjUrl, out var receiverKey));
		Assert.Equal("RK1Q0DJS3VVDXWQQTLQ8022QGXSX7ML9PHZ6EDSF6AKEWQG758JPS2EV", receiverKey);

		// A BIP 78 (v1) endpoint has no BIP 77 fragment params.
		Assert.False(Bip77UriParams.IsBip77("https://btcpay.example/BTC/pj"));
		Assert.False(Bip77UriParams.TryGetReceiverKey("https://btcpay.example/BTC/pj", out _));

		// A fragment without the receiver key is not BIP 77.
		Assert.False(Bip77UriParams.IsBip77("https://payjo.in/AbCd1234#EX1C4UC6ES"));
	}

	/// <summary>BIP 21 without a pj parameter yields no payjoin endpoint.</summary>
	[Fact]
	public void AddressParser_NoPjParameter_YieldsNullEndpoint()
	{
		var result = AddressParser.Parse("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=1", Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Null(uri.PayjoinEndpoint);
	}

	/// <summary>
	/// Kill-and-resume over the SQLite event log: a sender session created through
	/// <see cref="WalletWasabi.Payjoin.SenderSessionPersister"/> must replay back to the same
	/// typestate from a fresh store handle (fresh process, same DB file), fallback tx intact.
	/// </summary>
	[Fact]
	public async Task SenderSession_PersistAndReplay_ResumesState()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		string dbPath = Path.Combine(workDir, "sessions.sqlite");

		using var pjUri = PayjoinFfiTestHelpers.CreatePjUri();
		string endpoint = pjUri.PjEndpoint();

		// The dedup key parser must understand the fragment payjoin-ffi actually emits.
		Assert.True(Bip77UriParams.TryGetReceiverKey(endpoint, out var receiverKey));

		long sessionId;
		using (var store = PayjoinSenderSessionStore.FromFile(dbPath))
		{
			sessionId = store.CreateSession(endpoint, receiverKey, walletName: "test-wallet").Id;
			using var senderBuilder = new SenderBuilder(PayjoinFfiTestHelpers.OriginalPsbt, pjUri);
			using var initialTransition = senderBuilder.BuildRecommended(1000);
			using var withReplyKey = initialTransition.Save(new SenderSessionPersister(store, sessionId));
			Assert.NotNull(withReplyKey);
		}

		// "App restart": fresh connection over the same file.
		using (var store = PayjoinSenderSessionStore.FromFile(dbPath))
		{
			Assert.Equal(sessionId, Assert.Single(store.GetOpenSessions()).Id);

			using var replay = PayjoinMethods.ReplaySenderEventLog(new SenderSessionPersister(store, sessionId));
			using var state = replay.State();
			Assert.IsType<SendSession.WithReplyKey>(state);
			using var history = replay.SessionHistory();
			Assert.NotEmpty(history.FallbackTx());
		}
	}

	/// <summary>
	/// A receiver session persisted through <see cref="SqliteReceiverSessionPersister"/> must
	/// survive a "kill": a fresh store over the same database file replays the event log to
	/// the exact state (Initialized) and the payjoin URI round-trips.
	/// </summary>
	[Fact]
	public async Task ReceiverSession_PersistAndReplay_ResumesState()
	{
		string workDir = await Common.GetEmptyWorkDirAsync();
		string dbPath = Path.Combine(workDir, "Sessions.sqlite");
		const string ReceiveAddress = "tb1q6d3a2w975yny0asuvd9a67ner4nks58ff0q8g4";

		string sessionId;
		string pjUri;
		using (var store = PayjoinSessionStore.FromFile(dbPath))
		{
			sessionId = store.CreateSession("test-wallet", ReceiveAddress);
			var persister = new SqliteReceiverSessionPersister(store, sessionId);

			using var ohttpKeys = OhttpKeys.Decode(TestOhttpKeys);
			using var builder = new ReceiverBuilder(ReceiveAddress, "https://example.com", ohttpKeys);
			using var transition = builder.Build();
			using var initialized = transition.Save(persister);
			using var uri = initialized.PjUri();
			pjUri = uri.AsString();
		}

		// "Restart": a fresh store instance over the same database file.
		using (var store = PayjoinSessionStore.FromFile(dbPath))
		{
			var persister = new SqliteReceiverSessionPersister(store, sessionId);
			using var replay = PayjoinMethods.ReplayReceiverEventLog(persister);

			using var state = replay.State();
			Assert.IsType<ReceiveSession.Initialized>(state);

			using var history = replay.SessionHistory();
			using var replayedUri = history.PjUri();
			Assert.Equal(pjUri, replayedUri.AsString());
		}
	}

	/// <summary>OHTTP keys blob for tests, copied from payjoin-ffi's own xunit suite.</summary>
	internal static readonly byte[] TestOhttpKeys =
	[
		0x01, 0x00, 0x16, 0x04, 0xba, 0x48, 0xc4, 0x9c, 0x3d, 0x4a,
		0x92, 0xa3, 0xad, 0x00, 0xec, 0xc6, 0x3a, 0x02, 0x4d, 0xa1,
		0x0c, 0xed, 0x02, 0x18, 0x0c, 0x73, 0xec, 0x12, 0xd8, 0xa7,
		0xad, 0x2c, 0xc9, 0x1b, 0xb4, 0x83, 0x82, 0x4f, 0xe2, 0xbe,
		0xe8, 0xd2, 0x8b, 0xfe, 0x2e, 0xb2, 0xfc, 0x64, 0x53, 0xbc,
		0x4d, 0x31, 0xcd, 0x85, 0x1e, 0x8a, 0x65, 0x40, 0xe8, 0x6c,
		0x53, 0x82, 0xaf, 0x58, 0x8d, 0x37, 0x09, 0x57, 0x00, 0x04,
		0x00, 0x01, 0x00, 0x03,
	];

	/// <summary>
	/// Downgrade reasons must read as plain language. ffi error objects are pointer-backed
	/// and cannot be fabricated from C#, so the mappable ones are produced through the ffi
	/// itself; the well-known BIP 78 receiver error codes
	/// (unavailable/not-enough-money/version-unsupported/original-psbt-rejected) only occur
	/// on a live response and are exercised by the payjoin-cli integration harness.
	/// </summary>
	[Fact]
	public void PayjoinErrors_MapToUserFriendlyStrings()
	{
		// A BIP 21 without pj → real PjNotSupported from the ffi.
		var pjNotSupported = Assert.ThrowsAny<Exception>(() =>
		{
			using var ffiUri = global::Payjoin.Uri.Parse("bitcoin:2MuyMrZHkbHbfjudmKUy45dU4P17pjG2szK");
			using var pjUri = ffiUri.CheckPjSupported();
		});
		Assert.Equal(
			"The payjoin link could not be understood, so the payment was sent as a normal transaction.",
			Bip77PayjoinClient.FriendlyFfiMessage(pjNotSupported));

		// Anything unexpected reads generically, never as a raw ffi message.
		Assert.Equal(
			"Payjoin failed, so the payment was sent as a normal transaction.",
			Bip77PayjoinClient.FriendlyFfiMessage(new InvalidOperationException("rust panic goo")));

		// PayjoinException messages pass through ToUserFriendlyString verbatim — they are
		// authored user-facing (dedup, relay exhaustion, poll window).
		var duplicate = new PayjoinDuplicateSessionException(
			new PayjoinSenderSessionRecord(1, "https://payjo.in/x#RK1A", "RK1A", "w", null, DateTimeOffset.UtcNow, IsCompleted: false));
		Assert.Equal("A payjoin to this link is already in progress.", duplicate.ToUserFriendlyString());

		var completedDuplicate = new PayjoinDuplicateSessionException(
			new PayjoinSenderSessionRecord(1, "https://payjo.in/x#RK1A", "RK1A", "w", null, DateTimeOffset.UtcNow, IsCompleted: true));
		Assert.Contains("already completed", completedDuplicate.ToUserFriendlyString());
	}
}
