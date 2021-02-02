using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Tests.UnitTests;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class MempoolConsistencyTests
	{
		[Fact]
		public async Task SyncsMempoolAsync()
		{
			var rpc = new MockRpcClient();
			rpc.OnSendRawTransactionAsync = (tx) => Task.FromResult(tx.GetHash());
			rpc.OnGetRawMempoolAsync = () => Task.FromResult(Array.Empty<uint256>());
			using var bs = new MempoolConsistency(TimeSpan.FromHours(1), rpc, Network.TestNet);
			await bs.StartAsync(CancellationToken.None);
			await bs.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

			await bs.StopAsync(CancellationToken.None);
		}
	}
}
