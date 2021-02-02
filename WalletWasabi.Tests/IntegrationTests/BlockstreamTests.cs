using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.BlockstreamInfo;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class BlockstreamTests
	{
		[Fact]
		public void NoRegTest()
		{
			Assert.ThrowsAny<NotSupportedException>(() => new BlockstreamInfoClient(Network.RegTest));
		}

		[Fact]
		public async Task GetMempoolTransactionIdsAsync()
		{
			using var mainClient = new BlockstreamInfoClient(Network.Main);
			var mainIds = await mainClient.GetMempoolTransactionIdsAsync(CancellationToken.None);
			Assert.NotEmpty(mainIds);

			// Do it for testnet too, but don't assert, we have no idea.
			using var testClient = new BlockstreamInfoClient(Network.TestNet);
			var testIds = await testClient.GetMempoolTransactionIdsAsync(CancellationToken.None);
		}

		[Fact]
		public async Task GetTransactionAsync()
		{
			using var mainClient = new BlockstreamInfoClient(Network.Main);
			var mainTxId = new uint256("a841f7e4c2838ca945f20dcf43d22e78ea4cc8005302fde06c5d0b8f51ff11bd");
			var mainTx = await mainClient.GetTransactionAsync(mainTxId, CancellationToken.None);
			Assert.Equal(mainTxId, mainTx.GetHash());

			// Do it for testnet too, but don't assert, we have no idea.
			using var testClient = new BlockstreamInfoClient(Network.TestNet);
			var testTxId = new uint256("1e47c2d74b2e013fbabcb9d00b4eda4fd34c327d677431f9518f66d92ca3e7e5");
			var testTx = await testClient.GetTransactionAsync(testTxId, CancellationToken.None);
			Assert.Equal(testTxId, testTx.GetHash());
		}

		[Fact]
		public async Task GetTransactionFailsAsync()
		{
			using var client = new BlockstreamInfoClient(Network.Main);
			await Assert.ThrowsAnyAsync<HttpRequestException>(async () => await client.GetTransactionAsync(uint256.One, CancellationToken.None));
		}
	}
}
