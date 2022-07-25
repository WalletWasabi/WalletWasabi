using FluentAssertions;
using Microsoft.Reactive.Testing;
using ReactiveUI.Validation.Extensions;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AmountViewModelTests : ReactiveTest
{
	[Fact]
	public void Initial_state_is_invalid()
	{
		var sut = new AmountViewModel(_ => true);
		var testScheduler = new TestScheduler();
		var testableObserver = testScheduler.CreateObserver<bool>();
		sut.IsValid().Subscribe(testableObserver);

		testableObserver.Messages.Should().BeEquivalentTo(new[]
		{
			OnNext(0, false),
		});
	}

	[Fact]
	public void Setting_a_value_turns_valid()
	{
		var sut = new AmountViewModel(_ => true);
		var testScheduler = new TestScheduler();
		var testableObserver = testScheduler.CreateObserver<bool>();
		sut.IsValid().Subscribe(testableObserver);

		sut.Amount = 1;

		testableObserver.Messages.Should().BeEquivalentTo(new[]
		{
			OnNext(0, false),
			OnNext(0, true)
		});
	}

	[Fact]
	public void Setting_value_below_balance_is_invalid()
	{
		var sut = new AmountViewModel(_ => false);
		var testScheduler = new TestScheduler();
		var testableObserver = testScheduler.CreateObserver<bool>();
		sut.IsValid().Subscribe(testableObserver);

		sut.Amount = 1;

		testableObserver.Messages.Should().BeEquivalentTo(new[]
		{
			OnNext(0, false),
			OnNext(0, false)
		});
	}
}
