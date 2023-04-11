using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class TxPropagationVerifierTests
{
	[Fact]
	public async Task GetTransactionStatusTestAsync()
	{
		Mock<IHttpClientFactory> mockIHttpClientFactory = new(MockBehavior.Strict);
		mockIHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
		TxPropagationVerifier txPropagationVerifier = new(Network.Main, mockIHttpClientFactory.Object);

		foreach (var verifier in txPropagationVerifier.Verifiers)
		{
			// This TX exists
			uint256 txid = uint256.Parse("8a6edaae0ed93cf1a84fe727450be383ce53133df1a4438f9b9201b563ea9880");
			var status = await verifier.GetTransactionStatusAsync(txid, CancellationToken.None);
			Assert.NotNull(status);
			Assert.True(status);

			// This TX does not exist
			txid = uint256.Parse("8a6edaae0ed93cf1a84fe737450be383ce53133df1a4438f9b9201aaaaaaaaaa");
			status = await verifier.GetTransactionStatusAsync(txid, CancellationToken.None);
			Assert.Null(status);
		}
	}
}
