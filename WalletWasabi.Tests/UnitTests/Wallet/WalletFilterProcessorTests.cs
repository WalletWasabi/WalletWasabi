using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;
/// <summary>
/// Tests for wallet synchronization.
/// </summary>
/// <seealso cref="SyncType"/>
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

		await node.GenerateBlockAsync(testDeadlineCts.Token).ConfigureAwait(false);

		foreach (var _ in Enumerable.Range(0, 1000))
		{
			await node.GenerateBlockAsync(testDeadlineCts.Token).ConfigureAwait(false);
		}

		var allFilters = node.BuildFilters().ToList();

		// The MinGapLimit will generate some keys for both the Turbo and NonTurbo set.
		using var realWallet = await builder.CreateRealWalletBasedOnTestWalletAsync(wallet, 10000);

		// Unregister the event because on Wallet this is how it works: initial filters are processed without the event subscribed.
		realWallet.UnregisterNewFiltersEvent();

		// Process all but the last 4 which will be processed through events during the synchronization.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(allFilters.Take(allFilters.Count - 4).Where(x => x.Header.Height > 101));

		realWallet.BitcoinStore.IndexStore.NewFilters += (_, filters) => Wallet_NewFiltersEmulator(filters.Last().Header.Height, realWallet.WalletFilterProcessor);

		// Mock the database
		realWallet.WalletFilterProcessor.AddToCache(allFilters);

		await realWallet.WalletFilterProcessor.StartAsync(testDeadlineCts.Token).ConfigureAwait(false);

		// This emulates first synchronization
		var turboSyncTask = Task.Run(
			async () =>
			{
				await realWallet.WalletFilterProcessor.ProcessAsync(
					realWallet.BitcoinStore.SmartHeaderChain.TipHeight - 5,
					SyncType.Turbo,
					testDeadlineCts.Token);
			},
			testDeadlineCts.Token);

		// This emulates receiving some new filters while first synchronization is being processed.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 4) });
		var firstExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 3) });
		var secondExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);

		// Turbo sync should take some time.
		Assert.False(turboSyncTask.IsCompleted);

		// Turbo sync should finish first
		var swT = Stopwatch.StartNew();
		var lastTaskToComplete = await Task.WhenAny(turboSyncTask, firstExtraFilter.TurboTask, firstExtraFilter.NonTurboTask, secondExtraFilter.TurboTask, secondExtraFilter.NonTurboTask);
		swT.Stop();
		Assert.Equal(turboSyncTask.Id, lastTaskToComplete.Id);

		// This emulates final synchronization
		var nonTurboSyncTask = Task.Run(
			async () =>
			{
				await realWallet.WalletFilterProcessor.ProcessAsync(
					realWallet.BitcoinStore.SmartHeaderChain.TipHeight - 5,
					SyncType.NonTurbo,
					testDeadlineCts.Token);
			},
			testDeadlineCts.Token);

		// Turbo should be processed first in order of height
		lastTaskToComplete = await Task.WhenAny(nonTurboSyncTask, firstExtraFilter.TurboTask, firstExtraFilter.NonTurboTask, secondExtraFilter.TurboTask, secondExtraFilter.NonTurboTask);
		Assert.Equal(firstExtraFilter.TurboTask.Id, lastTaskToComplete.Id);
		lastTaskToComplete = await Task.WhenAny(nonTurboSyncTask, firstExtraFilter.NonTurboTask, secondExtraFilter.TurboTask, secondExtraFilter.NonTurboTask);
		Assert.Equal(secondExtraFilter.TurboTask.Id, lastTaskToComplete.Id);

		// This emulates receiving some new filters while final synchronization is being processed.
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 2) });
		var thirdExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);
		await realWallet.BitcoinStore.IndexStore.AddNewFiltersAsync(new List<FilterModel>() { allFilters.ElementAt(allFilters.Count - 1) });
		var fourthExtraFilter = await _eventChannel.Reader.ReadAsync(testDeadlineCts.Token);

		// Turbo should be gain priority.
		lastTaskToComplete = await Task.WhenAny(nonTurboSyncTask, firstExtraFilter.NonTurboTask, secondExtraFilter.NonTurboTask, thirdExtraFilter.TurboTask, thirdExtraFilter.NonTurboTask, fourthExtraFilter.TurboTask, fourthExtraFilter.NonTurboTask);
		Assert.Equal(thirdExtraFilter.TurboTask.Id, lastTaskToComplete.Id);
		lastTaskToComplete = await Task.WhenAny(nonTurboSyncTask, firstExtraFilter.NonTurboTask, secondExtraFilter.NonTurboTask, thirdExtraFilter.NonTurboTask, fourthExtraFilter.TurboTask, fourthExtraFilter.NonTurboTask);
		Assert.Equal(fourthExtraFilter.TurboTask.Id, lastTaskToComplete.Id);

		// Finally NonTurbo should end in order
		lastTaskToComplete = await Task.WhenAny(nonTurboSyncTask, firstExtraFilter.NonTurboTask, secondExtraFilter.NonTurboTask, thirdExtraFilter.NonTurboTask, fourthExtraFilter.NonTurboTask);
		Assert.Equal(nonTurboSyncTask.Id, lastTaskToComplete.Id);
		lastTaskToComplete = await Task.WhenAny(firstExtraFilter.NonTurboTask, secondExtraFilter.NonTurboTask, thirdExtraFilter.NonTurboTask, fourthExtraFilter.NonTurboTask);
		Assert.Equal(firstExtraFilter.NonTurboTask.Id, lastTaskToComplete.Id);
		lastTaskToComplete = await Task.WhenAny(secondExtraFilter.NonTurboTask, thirdExtraFilter.NonTurboTask, fourthExtraFilter.NonTurboTask);
		Assert.Equal(secondExtraFilter.NonTurboTask.Id, lastTaskToComplete.Id);
		lastTaskToComplete = await Task.WhenAny(thirdExtraFilter.NonTurboTask, fourthExtraFilter.NonTurboTask);
		Assert.Equal(thirdExtraFilter.NonTurboTask.Id, lastTaskToComplete.Id);
		await fourthExtraFilter.NonTurboTask;

		// Blockchain Tip should be reach for both SyncTypes.
		Assert.Equal(realWallet.BitcoinStore.SmartHeaderChain.TipHeight, (uint) realWallet.KeyManager.GetBestHeight().Value);
		Assert.Equal(realWallet.BitcoinStore.SmartHeaderChain.TipHeight, (uint) realWallet.KeyManager.GetBestTurboSyncHeight().Value);
	}

	// This emulates the NewFiltersProcessed event with SyncType separation to keep the track of the order.
	private void Wallet_NewFiltersEmulator(uint filterHeight, WalletFilterProcessor walletFilterProcessor)
	{
		// Initiate tasks and write tasks to the channel to pass them back to the test.
		// Underlying tasks without cancellation token as it works on Wallet.
		_eventChannel.Writer.TryWrite(
			new NewFiltersEventTasks(
			walletFilterProcessor.ProcessAsync(filterHeight, SyncType.Turbo, CancellationToken.None),
		walletFilterProcessor.ProcessAsync(filterHeight, SyncType.NonTurbo, CancellationToken.None)));
	}

	private record NewFiltersEventTasks(Task TurboTask, Task NonTurboTask);
}
