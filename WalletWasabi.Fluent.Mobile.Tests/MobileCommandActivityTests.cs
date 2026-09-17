using System.Reactive.Linq;
using System.Reactive.Subjects;
using WalletWasabi.Fluent.Mobile.ViewModels;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class MobileCommandActivityTests
{
	[Theory]
	[InlineData(false, false, false, false)]
	[InlineData(false, false, true, true)]
	[InlineData(false, true, false, true)]
	[InlineData(false, true, true, true)]
	[InlineData(true, false, false, true)]
	[InlineData(true, false, true, true)]
	[InlineData(true, true, false, true)]
	[InlineData(true, true, true, true)]
	public void EveryBusySourcePreventsEditing(bool page, bool confirm, bool alternate, bool expected)
	{
		bool? result = null;
		using var subscription = MobileCommandActivity.Combine(Observable.Return(page), Observable.Return(confirm), Observable.Return(alternate))
			.Subscribe(value => result = value);
		Assert.Equal(expected, result);
	}

	[Fact]
	public void PageFlagCannotUnlockAStillExecutingCommand()
	{
		using var page = new BehaviorSubject<bool>(false);
		using var confirm = new BehaviorSubject<bool>(false);
		using var alternate = new BehaviorSubject<bool>(false);
		var states = new List<bool>();
		using var subscription = MobileCommandActivity.Combine(page, confirm, alternate).Subscribe(states.Add);
		confirm.OnNext(true);
		page.OnNext(true);
		page.OnNext(false);
		Assert.True(states[^1]);
		alternate.OnNext(true);
		confirm.OnNext(false);
		Assert.True(states[^1]);
		alternate.OnNext(false);
		Assert.Equal(new[] { false, true, false }, states);
	}

	[Fact]
	public void DisposalReleasesAllThreeObservables()
	{
		using var page = new BehaviorSubject<bool>(false);
		using var confirm = new BehaviorSubject<bool>(false);
		using var alternate = new BehaviorSubject<bool>(false);
		var states = new List<bool>();
		var subscription = MobileCommandActivity.Combine(page, confirm, alternate).Subscribe(states.Add);
		Assert.True(page.HasObservers && confirm.HasObservers && alternate.HasObservers);
		subscription.Dispose();
		Assert.False(page.HasObservers || confirm.HasObservers || alternate.HasObservers);
		confirm.OnNext(true);
		Assert.Equal(new[] { false }, states);
	}

	[Fact]
	public void AbsentCommandsStillRespectTheViewModelBusyFlag()
	{
		using var page = new BehaviorSubject<bool>(false);
		var states = new List<bool>();
		using var subscription = MobileCommandActivity.Observe(page, null, null).Subscribe(states.Add);
		page.OnNext(true);
		page.OnNext(false);
		Assert.Equal(new[] { false, true, false }, states);
	}
}
