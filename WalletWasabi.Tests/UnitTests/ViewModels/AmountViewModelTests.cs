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
		sut.IsValid()
			.RecordChanges(() => { })
			.Should()
			.BeEquivalentTo(new[] { false });
	}

	[Fact]
	public void Any_amount_below_balance_is_valid()
	{
		var sut = new AmountViewModel(_ => true);
		sut.IsValid()
			.RecordChanges(() => sut.Amount = 1)
			.Should()
			.BeEquivalentTo(new[] { false, true });
	}

	[Fact]
	public void Any_amount_below_balance_is_invalid()
	{
		var sut = new AmountViewModel(_ => false);
		sut.IsValid()
			.RecordChanges(() => sut.Amount = 1)
			.Should()
			.BeEquivalentTo(new[] { false, false });
	}
}
