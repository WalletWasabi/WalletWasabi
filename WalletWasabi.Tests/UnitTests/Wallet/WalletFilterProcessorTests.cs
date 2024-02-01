using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.FilterProcessor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

/// <summary>
/// Tests for <see cref="WalletFilterProcessor"/>.
/// </summary>
public class WalletFilterProcessorTests
{
	private readonly Channel<NewFiltersEventTasks> _eventChannel = Channel.CreateUnbounded<NewFiltersEventTasks>();

	/// <summary>
	/// Verifies that the Comparer orders correctly the requests.
	/// </summary>
	[Fact]
	public void ComparerTest()
	{
		var requests = new List<WalletFilterProcessor.SyncRequest>()
		{
			new(SyncType.Turbo, new TaskCompletionSource()),
			new(SyncType.NonTurbo, new TaskCompletionSource()),
			new(SyncType.Complete, new TaskCompletionSource()),
			new(SyncType.NonTurbo, new TaskCompletionSource()),
			new(SyncType.Turbo, new TaskCompletionSource()),
			new(SyncType.NonTurbo, new TaskCompletionSource()),
			new(SyncType.Turbo, new TaskCompletionSource()),
		};

		PriorityQueue<WalletFilterProcessor.SyncRequest, Priority> synchronizationRequests = new(Priority.Comparer);

		foreach (var request in requests)
		{
			Priority priority = new(request.SyncType);
			synchronizationRequests.Enqueue(request, priority);
		}

		// Make sure that NonTurbo always has lower priority.
		while (synchronizationRequests.Count > 0)
		{
			var dequeuedRequest = synchronizationRequests.Dequeue();
			if (dequeuedRequest.SyncType == SyncType.NonTurbo)
			{
				// If we encounter a non-Turbo request, make sure there are no Turbo nor Complete requests left in the queue.
				Assert.DoesNotContain(SyncType.Turbo, synchronizationRequests.UnorderedItems.Select(x => x.Element.SyncType));
				Assert.DoesNotContain(SyncType.Complete, synchronizationRequests.UnorderedItems.Select(x => x.Element.SyncType));
			}
		}
	}

	[Fact]
	public async Task TestFilterProcessingAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		var node = await MockNode.CreateNodeAsync();
		var wallet = new TestWallet("wallet", node.Rpc);
		await using var builder = new WalletBuilder(node);

		await node.GenerateBlockAsync(testDeadlineCts.Token);

		foreach (var _ in Enumerable.Range(0, 1001))
		{
			await node.GenerateBlockAsync(testDeadlineCts.Token);
		}

		var allFilters = node.BuildFilters().ToList();

		// The MinGapLimit will generate some keys for both the Turbo and NonTurbo set.
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet, 2000);

		// Unregister the event because on Wallet this is how it works: initial filters are processed without the event subscribed.
		realWallet.UnregisterNewFiltersEvent();

		// Process all but the last 4 which will be processed through events during the synchronization.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(allFilters.Take(allFilters.Count - 4).Where(x => x.Header.Height > 101));

		realWallet.BitcoinStore.IndexStore.NewFilters += (_, filters) => Wallet_NewFiltersEmulator(realWallet.WalletFilterProcessor);

		// Mock the database
		foreach (SyncType syncType in Enum.GetValues<SyncType>())
		{
			foreach (var filter in allFilters)
			{
				realWallet.WalletFilterProcessor.FilterIteratorsBySyncType[syncType].Cache[filter.Header.Height] = filter;
			}
		}

		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token);

		List<Task> allTurboTasks = new();
		List<Task> allNonTurboTasks = new();

		// This emulates first synchronization
		var turboSyncTask = Task.Run(
			async () => await realWallet.WalletFilterProcessor.ProcessAsync(SyncType.Turbo, testDeadlineCts.Token),
			testDeadlineCts.Token);
		allTurboTasks.Add(turboSyncTask);

		// This emulates receiving some new filters while first synchronization is being processed.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 4) });
		var firstExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTurboTasks.Add(firstExtraFilter.TurboTask);
		allNonTurboTasks.Add(firstExtraFilter.NonTurboTask);

		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 3) });
		var secondExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTurboTasks.Add(secondExtraFilter.TurboTask);
		allNonTurboTasks.Add(secondExtraFilter.NonTurboTask);

		// Turbo sync should take some time.
		Assert.False(turboSyncTask.IsCompleted);

		await turboSyncTask;

		// This emulates final synchronization
		var nonTurboSyncTask = Task.Run(
			async () => await realWallet.WalletFilterProcessor.ProcessAsync(SyncType.NonTurbo, testDeadlineCts.Token),
			testDeadlineCts.Token);
		allNonTurboTasks.Add(nonTurboSyncTask);

		// This emulates receiving some new filters while final synchronization is being processed.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 2) });
		var thirdExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTurboTasks.Add(thirdExtraFilter.TurboTask);
		allNonTurboTasks.Add(thirdExtraFilter.NonTurboTask);

		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 1) });
		var fourthExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		allTurboTasks.Add(fourthExtraFilter.TurboTask);
		allNonTurboTasks.Add(fourthExtraFilter.NonTurboTask);

		// All Turbo should finish before NonTurbo
		var whenAllTurbo = Task.WhenAll(allTurboTasks);
		var whenAllNonTurbo = Task.WhenAll(allNonTurboTasks);
		var firstFinishingSyncType = await Task.WhenAny(whenAllTurbo, whenAllNonTurbo);
		Assert.Equal(firstFinishingSyncType.Id, whenAllTurbo.Id);

		// All tasks should finish
		await whenAllNonTurbo;

		// Blockchain Tip should be reach for both SyncTypes.
		Assert.Equal(realWallet.BitcoinStore.SmartHeaderChain.TipHeight, (uint)realWallet.KeyManager.GetBestHeight(SyncType.Complete).Value);
		Assert.Equal(realWallet.BitcoinStore.SmartHeaderChain.TipHeight, (uint)realWallet.KeyManager.GetBestHeight(SyncType.Turbo).Value);
	}

	// This emulates the NewFiltersProcessed event with SyncType separation to keep the track of the order.
	private void Wallet_NewFiltersEmulator(WalletFilterProcessor walletFilterProcessor)
	{
		// Initiate tasks and write tasks to the channel to pass them back to the test.
		// Underlying tasks without cancellation token as it works on Wallet.
		Task turboTask = walletFilterProcessor.ProcessAsync(SyncType.Turbo, CancellationToken.None);
		Task nonTurboTask = walletFilterProcessor.ProcessAsync(SyncType.NonTurbo, CancellationToken.None);
		_eventChannel.Writer.TryWrite(new NewFiltersEventTasks(turboTask, nonTurboTask));
	}

	private record NewFiltersEventTasks(Task TurboTask, Task NonTurboTask);
}
