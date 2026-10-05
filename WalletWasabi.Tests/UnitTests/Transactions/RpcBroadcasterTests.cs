using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Tests.UnitTests.Mocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class RpcBroadcasterTests
{
	[Fact]
	public async Task RejectionRetainsTheNodeReason()
	{
		const string reason = "insufficient fee, rejecting replacement";
		var rpc = new MockRpcClient
		{
			OnSendRawTransactionAsync = _ => throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, reason, null!)
		};
		var result = await new RpcBroadcaster(rpc).BroadcastAsync(new SmartTransaction(Network.RegTest.CreateTransaction(), Height.Mempool), CancellationToken.None);
		Assert.Equal(reason, result.Match(_ => "unexpected acceptance", error => Assert.IsType<BroadcastError.RpcError>(error).RpcErrorMessage));
	}
}
