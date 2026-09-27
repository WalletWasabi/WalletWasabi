using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Tests.UnitTests.Mocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.TransactionBroadcasting;

public class RpcBroadcasterTests
{
	[Theory]
	[InlineData("non-final")]
	[InlineData("bad-txns-inputs-missingorspent")]
	public async Task PreservesRpcRejectionReasonAsync(string reason)
	{
		var rpc = new MockRpcClient
		{
			OnSendRawTransactionAsync = _ => throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, reason, null)
		};
		var broadcaster = new RpcBroadcaster(rpc);
		var transaction = new SmartTransaction(Network.RegTest.CreateTransaction());

		var result = await broadcaster.BroadcastAsync(transaction, CancellationToken.None);

		Assert.False(result.IsOk);
		var error = Assert.IsType<BroadcastError.RpcError>(result.Error);
		Assert.Equal(reason, error.RpcErrorMessage);
	}
}
