using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Statistics;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class TxPropagationVerifierTests
{
	[Fact]
	public async Task IsTxAcceptedByNodeTestAsync()
	{
		Mock<IHttpClientFactory> mockIHttpClientFactory = new(MockBehavior.Strict);
		mockIHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(CreateHttpClientMock(true));
		TxPropagationVerifier txPropagationVerifier = new(Network.Main, mockIHttpClientFactory.Object);

		foreach (var verifier in txPropagationVerifier.Verifiers)
		{
			// This TX exists
			var txid = uint256.Parse("8a6edaae0ed93cf1a84fe727450be383ce53133df1a4438f9b9201b563ea9880");
			var status = await verifier.IsTxAcceptedByNode(txid, CancellationToken.None);
			Assert.True(status);
		}

		mockIHttpClientFactory = new(MockBehavior.Strict);
		mockIHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(CreateHttpClientMock(false));
		txPropagationVerifier = new(Network.Main, mockIHttpClientFactory.Object);

		foreach (var verifier in txPropagationVerifier.Verifiers)
		{
			// This TX does not exist
			var txid = uint256.Parse("8a6edaae0ed93cf1a84fe737450be383ce53133df1a4438f9b9201aaaaaaaaaa");
			var status = await verifier.IsTxAcceptedByNode(txid, CancellationToken.None);
			Assert.False(status);
		}
	}

	private HttpClient CreateHttpClientMock(bool txExists)
	{
		var mock = new Mock<HttpClient>(MockBehavior.Strict);
		mock.Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
				() =>
			{
				if (txExists)
				{
					var content = GenerateCleanJsonResponse();
					HttpResponseMessage response = new(System.Net.HttpStatusCode.OK);
					response.Content = new StringContent(content);
					return response;
				}
				return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
			});
		return mock.Object;
	}

	private string GenerateCleanJsonResponse()
	{
		return """{ "txid": "8a6edaae0ed93cf1a84fe727450be383ce53133df1a4438f9b9201b563ea9880", "version": 1, "locktime": 0, "vin": [ { "txid": "8e215ecfd2448fa0622b53c5ebb0c8c1f2a6de220cf110adc8bb7d0c2eb8e28e", "vout": 66, "prevout": { "scriptpubkey": "001499dc7d8636ed36bf9a99e50e12d691f274b7c9d0", "scriptpubkey_asm": "OP_0 OP_PUSHBYTES_20 99dc7d8636ed36bf9a99e50e12d691f274b7c9d0", "scriptpubkey_type": "v0_p2wpkh", "scriptpubkey_address": "bc1qn8w8mp3ka5mtlx5eu58p94537f6t0jws5r05uh", "value": 14348907 }, "scriptsig": "", "scriptsig_asm": "", "witness": [ "304402204d25a8b4dfb45f70df76767a765653018267c61a5a6dd700d0e357c16ff8cb16022013fef3022e960a661f21638ced5c65483713c310c250fa18e8338b6f4a543e9b01", "0390506abc3af8fed845370e6fb1c5d25f844436289776a1a5082730aa9c5bfdf6" ], "is_coinbase": false, "sequence": 4294967295 } ], "vout": [ { "scriptpubkey": "001417b63e66a52e69d0e88f9ca4676338deb8471cae", "scriptpubkey_asm": "OP_0 OP_PUSHBYTES_20 17b63e66a52e69d0e88f9ca4676338deb8471cae", "scriptpubkey_type": "v0_p2wpkh", "scriptpubkey_address": "bc1qz7mrue499e5ap6y0njjxwcecm6uyw89w4gmr8u", "value": 10000000 } ], "size": 36138, "weight": 83880, "fee": 87310, "status": { "confirmed": true, "block_height": 775996, "block_hash": "00000000000000000003b0122157b2661baedd3be05c45676fe3e7ecf58e6a1c", "block_time": 1676100974 } }""";
	}
}
