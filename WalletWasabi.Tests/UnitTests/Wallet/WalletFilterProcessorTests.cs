using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
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

	[Fact]
	public async Task TestFilterProcessingAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var node = await MockNode.CreateNodeAsync();
		var wallet = new TestWallet("wallet", node.Rpc);
		await using var builder = new WalletBuilder(node);

		foreach (var _ in Enumerable.Range(0, 1000))
		{
			await node.GenerateBlockAsync(testDeadlineCts.Token).ConfigureAwait(false);
		}

		var allFilters = node.BuildFilters().ToList();

		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet);

		// Unregister the event because on Wallet this is how it works: initial filters are processed without the event subscribed.
		realWallet.UnregisterNewFiltersEvent();

		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(allFilters.Where(x => x.Header.Height > 101));

		foreach (var _ in Enumerable.Range(0, 10000))
		{
			realWallet.KeyManager.GetNextReceiveKey("test");
		}

		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token).ConfigureAwait(false);
		realWallet.WalletFilterProcessor.AddToCache(allFilters);

		// This emulates first synchronization
		var turboTask = Task.Run(
			async () =>
			{
				await realWallet.WalletFilterProcessor.ProcessAsync(
					realWallet.BitcoinStore.SmartHeaderChain.TipHeight - 4,
					SyncType.Turbo,
					testDeadlineCts.Token);
			},
			testDeadlineCts.Token);

		// This emulates final synchronization
		var nonTurboTask = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), testDeadlineCts.Token);
				await realWallet.WalletFilterProcessor.ProcessAsync(
					realWallet.BitcoinStore.SmartHeaderChain.TipHeight - 4,
					SyncType.NonTurbo,
					testDeadlineCts.Token);
			},
			testDeadlineCts.Token);

		// This emulates receiving some new filters while first synchronization is being processed.
		var turboTask4 = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(200), testDeadlineCts.Token);
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 4).Header.Height, SyncType.Turbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		var nonTurboTask4 = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(300), testDeadlineCts.Token);
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 4).Header.Height, SyncType.NonTurbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		var turboTask3 = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(400), testDeadlineCts.Token);
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 3).Header.Height, SyncType.Turbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		var nonTurboTask3 = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500), testDeadlineCts.Token);
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 3).Header.Height, SyncType.NonTurbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		Assert.False(turboTask.IsCompleted); // Turbo sync should take some time.

		var lastTaskToReturn = await Task.WhenAny(turboTask, nonTurboTask, turboTask4, nonTurboTask4, turboTask3, nonTurboTask3);
		Assert.Equal(turboTask.Id, lastTaskToReturn.Id);

		lastTaskToReturn = await Task.WhenAny(nonTurboTask, turboTask4, nonTurboTask4, turboTask3, nonTurboTask3);
		Assert.Equal(turboTask4.Id, lastTaskToReturn.Id); // Turbo should always have priority against NonTurbo, ordered by height.

		lastTaskToReturn = await Task.WhenAny(nonTurboTask, nonTurboTask4, turboTask3, nonTurboTask3);
		Assert.Equal(turboTask3.Id, lastTaskToReturn.Id);

		// This emulates receiving some new filters while second synchronization is being processed.
		var turboTask2 = Task.Run(
			async () =>
			{
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 2).Header.Height, SyncType.Turbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		var nonTurboTask2 = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), testDeadlineCts.Token);
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 2).Header.Height, SyncType.NonTurbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		var turboTask1 = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(200), testDeadlineCts.Token);
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 1).Header.Height, SyncType.Turbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		var nonTurboTask1 = Task.Run(
			async () =>
			{
				await Task.Delay(TimeSpan.FromMilliseconds(300), testDeadlineCts.Token);
				await Wallet_NewFiltersEmulatorAsync(allFilters.ElementAt(allFilters.Count - 1).Header.Height, SyncType.NonTurbo, realWallet.WalletFilterProcessor);
			},
			testDeadlineCts.Token);

		lastTaskToReturn = await Task.WhenAny(nonTurboTask, nonTurboTask4, nonTurboTask3, turboTask2, nonTurboTask2, turboTask1, nonTurboTask1);
		Assert.Equal(turboTask2.Id, lastTaskToReturn.Id); // Turbo should still have priority even if final synchronization is ongoing.

		lastTaskToReturn = await Task.WhenAny(nonTurboTask, nonTurboTask4, nonTurboTask3, nonTurboTask2, turboTask1, nonTurboTask1);
		Assert.Equal(turboTask1.Id, lastTaskToReturn.Id);

		lastTaskToReturn = await Task.WhenAny(nonTurboTask, nonTurboTask4, nonTurboTask3, nonTurboTask2, nonTurboTask1);
		Assert.Equal(nonTurboTask.Id, lastTaskToReturn.Id); // NonTurbo should now return, ordered by height.

		lastTaskToReturn = await Task.WhenAny(nonTurboTask4, nonTurboTask3, nonTurboTask2, nonTurboTask1);
		Assert.Equal(nonTurboTask4.Id, lastTaskToReturn.Id);

		lastTaskToReturn = await Task.WhenAny(nonTurboTask3, nonTurboTask2, nonTurboTask1);
		Assert.Equal(nonTurboTask3.Id, lastTaskToReturn.Id);

		lastTaskToReturn = await Task.WhenAny(nonTurboTask2, nonTurboTask1);
		Assert.Equal(nonTurboTask2.Id, lastTaskToReturn.Id);

		lastTaskToReturn = await Task.WhenAny(nonTurboTask1);
		Assert.Equal(nonTurboTask1.Id, lastTaskToReturn.Id);

		Assert.Equal(realWallet.BitcoinStore.SmartHeaderChain.TipHeight, (uint) realWallet.KeyManager.GetBestHeight().Value);
		Assert.Equal(realWallet.BitcoinStore.SmartHeaderChain.TipHeight, (uint) realWallet.KeyManager.GetBestTurboSyncHeight().Value);
	}

	private static async Task Wallet_NewFiltersEmulatorAsync(uint filterHeight, SyncType syncType, WalletFilterProcessor walletFilterProcessor)
	{
		// Underlying task without cancellation token as it works on Wallet.
		await walletFilterProcessor.ProcessAsync(filterHeight, syncType, CancellationToken.None).ConfigureAwait(false);
	}
}
