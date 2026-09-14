using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using WalletWasabi.Fluent.Mobile.ViewModels;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class FeePreferenceConsistencyTests
{
	private static MobileFeeTargetQuote[] NormalQuotes() =>
	[
		new(6, 6, 2),
		new(3, 3, 4),
		new(1, 1, 8)
	];

	[Theory]
	[InlineData(144)]
	[InlineData(2)]
	[InlineData(int.MaxValue)]
	[InlineData(int.MinValue)]
	public void UnmatchedPreferenceDoesNotHighlightOrPersistANearbyTarget(int preferred)
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		var writes = new List<int>();
		using var selection = new MobileSendFeeSelection(preferred, writes.Add, quotes, scheduler);
		quotes.OnNext(NormalQuotes());
		scheduler.Start();
		Assert.All(selection.Options, option => Assert.False(option.IsSelected));
		Assert.Empty(writes);
	}

	[Fact]
	public void RequotingASelectedCardCannotSilentlyChangeItsSavedTarget()
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		var writes = new List<int>();
		using var selection = new MobileSendFeeSelection(3, writes.Add, quotes, scheduler);
		quotes.OnNext(NormalQuotes());
		scheduler.Start();
		var priority = selection.Options.Single(x => x.RequestedBlocks == 1);
		priority.Execute(null);
		Assert.True(priority.IsSelected);
		quotes.OnNext([new(6, 6, 2), new(3, 3, 4), new(1, 2, 7)]);
		scheduler.Start();
		Assert.Empty(selection.Options.Where(x => x.IsSelected));
		Assert.Equal(new[] { 1 }, writes);
		Assert.Contains("saved target is 1 blocks", selection.Status);
		priority.Execute(null);
		Assert.True(priority.IsSelected);
		Assert.Equal(new[] { 1, 2 }, writes);
	}

	[Fact]
	public void FailedSaveKeepsPreviouslyCommittedSelection()
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		using var selection = new MobileSendFeeSelection(3, _ => throw new InvalidOperationException("read only"), quotes, scheduler);
		quotes.OnNext(NormalQuotes());
		scheduler.Start();
		selection.Options.Single(x => x.RequestedBlocks == 1).Execute(null);
		Assert.True(selection.Options.Single(x => x.RequestedBlocks == 3).IsSelected);
		Assert.False(selection.Options.Single(x => x.RequestedBlocks == 1).IsSelected);
		Assert.Contains("Could not save", selection.Status);
	}

	[Fact]
	public void RestoredQuotesRecoverOnlyAnExactSavedTarget()
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		var writes = new List<int>();
		using var selection = new MobileSendFeeSelection(3, writes.Add, quotes, scheduler);
		quotes.OnNext(NormalQuotes());
		scheduler.Start();
		quotes.OnNext([]);
		scheduler.Start();
		Assert.Empty(selection.Options.Where(x => x.IsSelected));
		quotes.OnNext(NormalQuotes());
		scheduler.Start();
		Assert.Equal(3, Assert.Single(selection.Options.Where(x => x.IsSelected)).TargetBlocks);
		Assert.Empty(writes);
	}

	[Fact]
	public void DuplicateQuotesDisableThatCardRatherThanChoosingAnArbitraryRate()
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		using var selection = new MobileSendFeeSelection(3, _ => { }, quotes, scheduler);
		quotes.OnNext([new(3, 3, 4), new(3, 3, 40)]);
		scheduler.Start();
		var standard = selection.Options.Single(x => x.RequestedBlocks == 3);
		Assert.False(standard.CanExecute(null));
		Assert.False(standard.IsSelected);
	}
}
