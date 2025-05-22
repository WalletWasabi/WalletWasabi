using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinRpc;

public record ConnectedRpcStatus(
	ulong Headers,
	ulong Blocks,
	Result<int, RPCResponse> PeersCount,
	uint256 BestBlockHash,
	bool Pruned,
	bool InitialBlockDownload)
{
	public override string? ToString() =>
		PeersCount.Match(
			peersCount => (peersCount, Synchronized) switch
			{
				(0, _) => "is connecting...",
				(_, true) => "is synchronized",
				(_, false) => "is synchronizing..."
			},
			response => (int) response.Error.Code switch
			{
				-32075 => "is connected with restrictions",
				_ => response.Error.Message
			});

	public bool Synchronized =>  Blocks == Headers;
}
