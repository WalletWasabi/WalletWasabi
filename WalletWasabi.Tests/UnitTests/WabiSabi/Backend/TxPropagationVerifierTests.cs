using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Statistics;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class TxPropagationVerifierTests
{
	[Fact]
	public async Task GetTransactionStatusTestAsync()
	{
		HttpClient httpClient = new();
		TxPropagationVerifier txPropagationVerifier = new(Network.Main, httpClient);

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

		httpClient.Dispose();
	}
}
