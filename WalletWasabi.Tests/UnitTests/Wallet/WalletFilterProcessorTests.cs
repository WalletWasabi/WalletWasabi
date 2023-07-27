using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;
/// <summary>
/// Tests for wallet synchronization.
/// </summary>
/// <seealso cref="SyncType"/>
public class WalletFilterProcessorTests
{
	/// <summary>
	/// Verifies that the Comparer orders correctly the requests.
	/// </summary>
	[Fact]
	public void ComparerTest()
	{
		var requests = new List<WalletFilterProcessor.SyncRequest>()
		{
			new(SyncType.NonTurbo, 6, new TaskCompletionSource()),
			new(SyncType.Turbo, 2, new TaskCompletionSource()),
			new(SyncType.Complete, 3, new TaskCompletionSource()),
			new(SyncType.NonTurbo, 5, new TaskCompletionSource()),
			new(SyncType.Turbo, 4, new TaskCompletionSource()),
			new(SyncType.NonTurbo, 4, new TaskCompletionSource()),
			new(SyncType.Turbo, 1, new TaskCompletionSource()),
		};

		PriorityQueue<WalletFilterProcessor.SyncRequest, WalletFilterProcessor.Priority> synchronizationRequests = new(WalletFilterProcessor.Comparer);

		foreach (var request in requests)
		{
			WalletFilterProcessor.Priority priority = new(request.SyncType, request.Height);
			synchronizationRequests.Enqueue(request, priority);
		}

		var currentItem = synchronizationRequests.Dequeue();
		Assert.Equal((uint)1, currentItem.Height);
		Assert.Equal(SyncType.Turbo, currentItem.SyncType);

		currentItem = synchronizationRequests.Dequeue();
		Assert.Equal((uint)2, currentItem.Height);
		Assert.Equal(SyncType.Turbo, currentItem.SyncType);

		currentItem = synchronizationRequests.Dequeue();
		Assert.Equal((uint)3, currentItem.Height);
		Assert.Equal(SyncType.Complete, currentItem.SyncType);

		currentItem = synchronizationRequests.Dequeue();
		Assert.Equal((uint)4, currentItem.Height);
		Assert.Equal(SyncType.Turbo, currentItem.SyncType);

		currentItem = synchronizationRequests.Dequeue();
		Assert.Equal((uint)4, currentItem.Height);
		Assert.Equal(SyncType.NonTurbo, currentItem.SyncType);

		currentItem = synchronizationRequests.Dequeue();
		Assert.Equal((uint)5, currentItem.Height);
		Assert.Equal(SyncType.NonTurbo, currentItem.SyncType);
	}
}
