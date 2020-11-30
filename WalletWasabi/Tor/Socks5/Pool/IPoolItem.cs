using System.IO;

namespace WalletWasabi.Tor.Socks5.Pool
{
	public interface IPoolItem
	{
		bool NeedRecycling();
		bool TryReserve();

		PoolItemState Unreserve();

		Stream GetTransportStream();
	}
}
