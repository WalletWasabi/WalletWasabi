using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using WalletWasabi.Fluent.Mobile.ViewModels;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class FeeSelectionTests
{
	[Fact]
	public void LoadingQuotesDoesNotPersistASelection()
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		var writes = new List<int>();
		using var selection = new MobileSendFeeSelection(3, writes.Add, quotes, scheduler);
		Assert.All(selection.Options, option => Assert.False(option.SelectCommand.CanExecute(null)));
		quotes.OnNext(new[] { new MobileFeeTargetQuote(6, 6, 2), new MobileFeeTargetQuote(3, 3, 4), new MobileFeeTargetQuote(1, 1, 8) });
		scheduler.Start();
		Assert.Empty(writes);
		Assert.True(selection.Options.Single(x => x.RequestedBlocks == 3).IsSelected);
		var priority = selection.Options.Single(x => x.RequestedBlocks == 1);
		priority.SelectCommand.Execute(null);
		Assert.Equal(new[] { 1 }, writes);
		Assert.Single(selection.Options, x => x.IsSelected);
	}

	[Fact]
	public void ClampedTargetsSelectOnlyTheTappedCard()
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		var writes = new List<int>();
		using var selection = new MobileSendFeeSelection(3, writes.Add, quotes, scheduler);
		quotes.OnNext(new[] { new MobileFeeTargetQuote(6, 6, 2), new MobileFeeTargetQuote(3, 6, 2), new MobileFeeTargetQuote(1, 6, 2) });
		scheduler.Start();
		selection.Options.Single(x => x.RequestedBlocks == 1).SelectCommand.Execute(null);
		Assert.Equal(new[] { 6 }, writes);
		Assert.Single(selection.Options, x => x.IsSelected);
		Assert.True(selection.Options.Single(x => x.RequestedBlocks == 1).IsSelected);
	}

	[Fact]
	public void InvalidQuotesAndDisposalCannotChangeFeePreferences()
	{
		var scheduler = new HistoricalScheduler();
		using var quotes = new Subject<MobileFeeTargetQuote[]>();
		var writes = new List<int>();
		var selection = new MobileSendFeeSelection(3, writes.Add, quotes, scheduler);
		quotes.OnNext(new[] { new MobileFeeTargetQuote(6, 0, 2), new MobileFeeTargetQuote(3, 3, -1), new MobileFeeTargetQuote(1, 1, 0) });
		scheduler.Start();
		foreach (var option in selection.Options) option.SelectCommand.Execute(null);
		Assert.Empty(writes);
		selection.Dispose();
		Assert.False(quotes.HasObservers);
		Assert.All(selection.Options, option => Assert.False(option.SelectCommand.CanExecute(null)));
	}
}
