using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Payjoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.WebClients.PayJoin;
using Xunit;
using Uri = System.Uri;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// BIP 77 sender failure paths over a mocked HttpClient. Happy-path proposals need valid
/// OHTTP-encapsulated directory responses that cannot be fabricated offline; the round trip
/// against a real directory/relay is covered by the payjoin-cli integration harness.
/// </summary>
public class Bip77PayjoinClientTests
{
	private static (Bip77PayjoinClient Client, PayjoinSenderSessionStore Store, string Endpoint, string ReceiverKey, WalletWasabi.Blockchain.Transactions.TransactionFactory Factory, PaymentIntent Payment)
		CreateHarness(MockHttpClient mockHttpClient)
	{
		var factory = ServiceFactory.CreateTransactionFactory(
		[
			("Pablo", 0, 0.1m, true, 1),
		]);

		using Key destinationKey = new();
		var destination = destinationKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, Network.Main);

		using var pjUri = PayjoinFfiTestHelpers.CreatePjUri(destination.ToString());
		string endpoint = pjUri.PjEndpoint();
		Assert.True(Bip77UriParams.TryGetReceiverKey(endpoint, out var receiverKey));

		string bip21 = $"bitcoin:{destination}?amount=0.001&pj={Uri.EscapeDataString(endpoint)}";

		var store = PayjoinSenderSessionStore.FromFile(":memory:");
		var client = new Bip77PayjoinClient(
			bip21,
			endpoint,
			store,
			_ => mockHttpClient,
			walletName: "test-wallet",
			Network.Main,
			ohttpRelays: ["https://relay-a.example", "https://relay-b.example"],
			pollWindow: TimeSpan.FromSeconds(30));

		var payment = new PaymentIntent(destination, Money.Coins(0.001m));

		return (client, store, endpoint, receiverKey, factory, payment);
	}

	private static BuildTransactionResult BuildTransaction(WalletWasabi.Blockchain.Transactions.TransactionFactory factory, PaymentIntent payment, IPayjoinClient client)
	{
		var txParameters = TransactionParametersBuilder.CreateDefault()
			.SetFeeRate(2)
			.SetAllowUnconfirmed(true)
			.SetPayment(payment)
			.SetAllowedInputs(factory.Coins.Select(x => x.Outpoint))
			.Build();

		return factory.BuildTransaction(txParameters, payjoinClient: client);
	}

	[Fact]
	public void AllRelaysUnreachable_DegradesToPlainSendAndClosesSession()
	{
		int attempts = 0;
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ =>
		{
			attempts++;
			return Task.FromException<HttpResponseMessage>(new HttpRequestException("no route"));
		};

		var (client, store, endpoint, receiverKey, factory, payment) = CreateHarness(mockHttpClient);
		using PayjoinSenderSessionStore storeDisposal = store;

		var tx = BuildTransaction(factory, payment, client);

		// The send degraded to the plain path but still produced a signed tx.
		Assert.True(tx.Signed);
		Assert.Equal(2, attempts); // Both relays were tried before giving up.
		Assert.Equal("None of the payjoin relays could be reached.", client.DowngradeReason);

		// The session was recorded, closed, and its fallback tx is the tx we actually built.
		Assert.True(store.TryFindSession(endpoint, receiverKey, out var session));
		Assert.True(session.IsCompleted);
		Assert.NotNull(session.FallbackTxHex);
		var fallbackTx = Transaction.Parse(session.FallbackTxHex, Network.Main);
		Assert.Equal(tx.Transaction.Transaction.GetHash(), fallbackTx.GetHash());
	}

	[Fact]
	public void GarbageRelayResponse_DegradesToPlainSendAndClosesSession()
	{
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ =>
		{
			var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
			{
				Content = new ByteArrayContent([0xde, 0xad, 0xbe, 0xef]),
			};
			return Task.FromResult(response);
		};

		var (client, store, endpoint, receiverKey, factory, payment) = CreateHarness(mockHttpClient);
		using PayjoinSenderSessionStore storeDisposal = store;

		var tx = BuildTransaction(factory, payment, client);

		Assert.True(tx.Signed);
		Assert.NotNull(client.DowngradeReason);
		Assert.True(store.TryFindSession(endpoint, receiverKey, out var session));
		Assert.True(session.IsCompleted);
	}

	[Fact]
	public void AlreadyUsedPayjoinUri_DegradesWithoutAnyNetworkTraffic()
	{
		int attempts = 0;
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = _ =>
		{
			attempts++;
			return Task.FromException<HttpResponseMessage>(new HttpRequestException("no route"));
		};

		var (client, store, endpoint, receiverKey, factory, payment) = CreateHarness(mockHttpClient);
		using PayjoinSenderSessionStore storeDisposal = store;

		// The same receiver key was used before — even though that session completed,
		// re-arming it must be refused (address/HPKE-key reuse).
		var previous = store.CreateSession(endpoint, receiverKey, "test-wallet");
		store.CompleteSession(previous.Id);

		var tx = BuildTransaction(factory, payment, client);

		Assert.True(tx.Signed);
		Assert.Equal(0, attempts);
		Assert.Contains("already completed", client.DowngradeReason);
	}
}
