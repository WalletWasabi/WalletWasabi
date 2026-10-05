using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class MempoolTorRoutingTests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task CpfpUsesTheSelectedTransportAndNetworkAsync(bool tor, bool testnet)
	{
		Uri? requested = null;
		using var client = new MockHttpClient();
		client.OnSendAsync = request =>
		{
			requested = new Uri(client.BaseAddress!, request.RequestUri!);
			return Task.FromResult(HttpResponseMessageEx.Ok("""{"effectiveFeePerVsize":10,"fee":1000,"adjustedVsize":100,"ancestors":[]}"""));
		};
		IHttpClientFactory factory = tor
			? new TorFactory(client)
			: new MockHttpClientFactory { OnCreateClient = _ => client };
		var handler = CpfpInfoUpdater.Create(factory, testnet ? Network.TestNet4 : Network.Main, new EventBus());
		var transaction = BitcoinFactory.CreateSmartTransaction(height: Height.Mempool);
		var reply = new TestReplyChannel<Result<CpfpInfo, string>>();
		await handler(new CpfpInfoMessage.GetInfoForTransaction(transaction, reply), Unit.Instance, CancellationToken.None);
		Assert.True(reply.Result!.IsOk);
		Assert.NotNull(requested);
		Assert.Equal(tor ? "http" : "https", requested.Scheme);
		Assert.Equal(tor ? "mempoolhqx4isw62xs7abwphsq7ldayuidyx2v2oethdhhj6mlo2r6ad.onion" : "mempool.space", requested.Host);
		Assert.Equal((testnet ? "/testnet4" : "") + "/api/v1/cpfp/" + transaction.GetHash(), requested.AbsolutePath);
	}

	private sealed class TorFactory(HttpClient client) : OnionHttpClientFactory(new Uri("socks5://127.0.0.1:9050")), IHttpClientFactory
	{
		public new HttpClient CreateClient(string name) => client;
	}
}
