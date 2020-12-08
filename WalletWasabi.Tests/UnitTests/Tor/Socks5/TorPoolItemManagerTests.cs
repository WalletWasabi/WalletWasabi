using System;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5
{
	/// <summary>
	/// Tests for <see cref="TorPoolItemManager"/>
	/// </summary>
	public class TorPoolItemManagerTests
	{
		[Fact]
		public void BehaviorTest()
		{
			using TorPoolItemManager clientsManager = new(maxPoolItemsPerHost: 2);

			// No items are stored.
			Uri uri = new("https://postman-echo.com");
			Assert.Empty(clientsManager.GetItemsCopy(uri.Host));

			// No pool item can be re-used at this point.
			(bool canBeAdded, IPoolItem? poolItem) result1 = clientsManager.GetPoolItem(uri.Host, isolateStream: false);
			Assert.True(result1.canBeAdded);
			Assert.Null(result1.poolItem);

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
			(bool canBeAdded, IPoolItem? poolItem) result2 = clientsManager.GetPoolItem(uri.Host, isolateStream: false);
			Assert.False(result2.canBeAdded); // All slots are full.
			Assert.NotNull(result2.poolItem);
			Assert.Equal(item1, result2.poolItem);
			Assert.Equal(PoolItemState.InUse, ((TestPoolItem)result2.poolItem!).State);
		}
	}
}