using System;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5
{
	/// <summary>
	/// Tests for <see cref="TorPoolItemManager"/>.
	/// </summary>
	public class TorPoolItemManagerTests
	{
		/// <summary>
		/// Make sure that at most 2 TCP connections are established with Tor SOCKS5 for a given URI host (e.g. <c>postman-echo.com</c>).
		/// </summary>
		[Fact]
		public void BehaviorTest()
		{
			using TorPoolItemManager clientsManager = new(maxPoolItemsPerHost: 2);

			// No items are stored.
			Uri uri = new("https://postman-echo.com");
			Assert.Empty(clientsManager.GetItemsCopy(uri.Host));

			// No pool item can be re-used at this point.
			bool canBeAdded = clientsManager.GetPoolItem(uri.Host, isolateStream: false, out IPoolItem? poolItem);
			Assert.True(canBeAdded);
			Assert.Null(poolItem);

			// No pool item can be re-used at this point.
			TestPoolItem item1 = new(PoolItemState.InUse, allowRecycling: true);
			bool itemAdded = clientsManager.TryAddPoolItem(uri.Host, item1);
			Assert.True(itemAdded);

			// One added pool item is in used, so it cannot be re-used.
			TestPoolItem item2 = new(PoolItemState.InUse, allowRecycling: true);
			bool itemAdded2 = clientsManager.TryAddPoolItem(uri.Host, item2);
			Assert.True(itemAdded2);

			// Cannot add third item, limit is two per host.
			TestPoolItem item3 = new(PoolItemState.InUse, allowRecycling: true);
			bool itemAdded3 = clientsManager.TryAddPoolItem(uri.Host, item3);
			Assert.False(itemAdded3);

			// Change state of item1, so that is free to be used again.
			item1.State = PoolItemState.FreeToUse;

			// Get free pool item, it should be item1.
			canBeAdded = clientsManager.GetPoolItem(uri.Host, isolateStream: false, out poolItem);
			Assert.False(canBeAdded); // All slots are full.
			Assert.NotNull(poolItem);
			Assert.Equal(item1, poolItem);
			Assert.Equal(PoolItemState.InUse, ((TestPoolItem)poolItem!).State);
		}
	}
}