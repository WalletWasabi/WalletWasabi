using NBitcoin;

namespace WalletWasabi.BitcoinRpc;

public abstract record RpcStatus
{
	public record Unresponsive : RpcStatus;

	public record Responsive(ulong Headers, ulong Blocks, int PeersCount, uint256 BestBlockHash, bool Pruned, bool InitialBlockDownload) : RpcStatus;
	public bool Synchronized => this is Responsive r && r.Blocks == r.Headers;

	public override string ToString()
	{
		if (this is Responsive r)
		{
			if (r.PeersCount == 0)
			{
				return "is connecting...";
			}

			return Synchronized ? "is synchronized" : "is synchronizing...";
		}
		return "is unresponsive";
	}
}
