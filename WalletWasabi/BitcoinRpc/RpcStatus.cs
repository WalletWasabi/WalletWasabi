using NBitcoin;

namespace WalletWasabi.BitcoinRpc;

public abstract record RpcStatus
{
	public record Unresponsive : RpcStatus
	{
		public override string? ToString() => "is unresponsive";
	}

	public record Responsive(
		ulong Headers,
		ulong Blocks,
		int PeersCount,
		uint256 BestBlockHash,
		bool Pruned,
		bool InitialBlockDownload) : RpcStatus
	{
		public override string? ToString() =>
			(PeersCount, Synchronized) switch
			{
				(0, _) => "is connecting...",
				(_, true) => "is synchronized",
				(_, false) => "is synchronizing..."
			};
	}

	public bool Synchronized => this is Responsive r && r.Blocks == r.Headers;

}
