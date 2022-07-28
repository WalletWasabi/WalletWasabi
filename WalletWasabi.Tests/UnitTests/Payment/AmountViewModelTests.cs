using ReactiveUI.Validation.Extensions;
using WalletWasabi.Fluent.Controls.Payment.ViewModels;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Payment;

public class AmountViewModelTests
{
	[Fact]
	public void Initial_state_is_invalid()
	{
		var sut = new AmountViewModel(_ => true);

		Assert.Equal(sut.IsValid().RecordChanges(), new[] { false });
	}

	[Fact]
	public void Any_amount_below_balance_is_valid()
	{
		var sut = new AmountViewModel(_ => true);
		Assert.Equal(sut.IsValid().RecordChanges(() => sut.Amount = 1), new[] { false, true });
	}

	[Fact]
	public void Any_amount_below_balance_is_invalid()
	{
		var sut = new AmountViewModel(_ => false);
		Assert.Equal(sut.IsValid().RecordChanges(() => sut.Amount = 1), new[] { false, false });
	}
}
