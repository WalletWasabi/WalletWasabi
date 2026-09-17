using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using NBitcoin;
using WalletWasabi.Fluent.Mobile.ViewModels;
using WalletWasabi.Fluent.Models.Wallets;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileTransactionNavigationTests
{
	[Fact]
	public void RepeatedSelectionHasDistinctIdentityAndOnlyTheLatestCanBeConsumed()
	{
		var state = new TransactionSelection();
		var first = state.Request(uint256.One);
		var second = state.Request(uint256.One);
		Assert.NotSame(first, second);
		Assert.False(state.TryConsume(first));
		Assert.Same(second, state.Pending);
		Assert.True(state.TryConsume(second));
		Assert.Null(state.Pending);
		Assert.False(state.TryConsume(second));
	}

	[Fact]
	public void RequestBeforeViewAttachmentWaitsForTheActualRow()
	{
		using var harness = new Harness();
		var request = harness.Selection.Request(uint256.One);
		harness.Attach();
		harness.Scheduler.Start();
		Assert.Equal(1, harness.Preparations);
		Assert.Same(request, harness.Selection.Pending);
		Assert.Empty(harness.Opened);
		harness.Available.Add(uint256.One);
		harness.Rows.OnNext(Unit.Default);
		harness.Rows.OnNext(Unit.Default);
		harness.Scheduler.Start();
		Assert.Equal(new[] { uint256.One }, harness.Opened);
		Assert.Null(harness.Selection.Pending);
	}

	[Fact]
	public void DeliberateNavigationCancelsAWaitingSelection()
	{
		using var harness = new Harness();
		harness.Attach();
		harness.Selection.Request(uint256.One);
		harness.Scheduler.Start();
		harness.UserNavigation.OnNext(Unit.Default);
		harness.Available.Add(uint256.One);
		harness.Scheduler.Start();
		harness.Rows.OnNext(Unit.Default);
		harness.Scheduler.Start();
		Assert.Empty(harness.Opened);
		Assert.Null(harness.Selection.Pending);
	}

	[Fact]
	public void EarlierUserNavigationDoesNotCancelANewerSelection()
	{
		using var harness = new Harness();
		harness.Attach();
		harness.Selection.Request(uint256.One);
		harness.Scheduler.Start();
		harness.UserNavigation.OnNext(Unit.Default);
		var newerId = new uint256(2);
		harness.Available.Add(newerId);
		harness.Selection.Request(newerId);
		harness.Scheduler.Start();
		Assert.Equal(new[] { newerId }, harness.Opened);
	}

	[Fact]
	public void SupersededRequestCannotOpenWhenItsRowArrivesLater()
	{
		using var harness = new Harness();
		harness.Attach();
		harness.Selection.Request(uint256.One);
		harness.Scheduler.Start();
		var newerId = new uint256(2);
		harness.Selection.Request(newerId);
		harness.Available.Add(uint256.One);
		harness.Rows.OnNext(Unit.Default);
		harness.Scheduler.Start();
		Assert.Empty(harness.Opened);
		harness.Available.Add(newerId);
		harness.Rows.OnNext(Unit.Default);
		harness.Scheduler.Start();
		Assert.Equal(new[] { newerId }, harness.Opened);
	}

	[Fact]
	public void DisposedViewCannotConsumeOrOpenAQueuedRequest()
	{
		using var harness = new Harness();
		harness.Attach();
		var request = harness.Selection.Request(uint256.One);
		harness.Available.Add(uint256.One);
		harness.Detach();
		harness.Scheduler.Start();
		Assert.Empty(harness.Opened);
		Assert.Same(request, harness.Selection.Pending);
		Assert.False(harness.Rows.HasObservers);
		Assert.False(harness.UserNavigation.HasObservers);
		harness.Attach();
		harness.Scheduler.Start();
		Assert.Equal(new[] { uint256.One }, harness.Opened);
	}

	[Fact]
	public void UserNavigationWinsOverAnAlreadyQueuedRowRefresh()
	{
		using var harness = new Harness();
		harness.Attach();
		harness.Selection.Request(uint256.One);
		harness.Scheduler.Start();
		harness.Available.Add(uint256.One);
		harness.Rows.OnNext(Unit.Default);
		harness.UserNavigation.OnNext(Unit.Default);
		harness.Scheduler.Start();
		Assert.Empty(harness.Opened);
		Assert.Null(harness.Selection.Pending);
	}

	[Fact]
	public void DifferentWalletRequestsRemainIndependent()
	{
		using var first = new Harness();
		using var second = new Harness();
		first.Attach(); second.Attach();
		first.Available.Add(uint256.One); second.Available.Add(uint256.One);
		first.Selection.Request(uint256.One);
		first.Scheduler.Start(); second.Scheduler.Start();
		Assert.Single(first.Opened);
		Assert.Empty(second.Opened);
	}

	private sealed class Harness : IDisposable
	{
		private MobileTransactionNavigation? _binding;
		public TransactionSelection Selection { get; } = new();
		public HistoricalScheduler Scheduler { get; } = new();
		public Subject<Unit> Rows { get; } = new();
		public Subject<Unit> UserNavigation { get; } = new();
		public HashSet<uint256> Available { get; } = new();
		public List<uint256> Opened { get; } = new();
		public int Preparations { get; private set; }

		public void Attach()
		{
			_binding = new MobileTransactionNavigation(Selection,
				() => { Preparations++; UserNavigation.OnNext(Unit.Default); Rows.OnNext(Unit.Default); },
				id => Available.Contains(id) ? () => { Opened.Add(id); UserNavigation.OnNext(Unit.Default); Rows.OnNext(Unit.Default); } : null,
				Rows, UserNavigation, Scheduler);
		}

		public void Detach() { _binding?.Dispose(); _binding = null; }
		public void Dispose() { Detach(); Rows.Dispose(); UserNavigation.Dispose(); }
	}
}
