using FluentAssertions;
using NBitcoin;
using WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class AddressTests
{
	private const string BtcAddress = "tb1q0382a3m2jzvyk5lkea5h5jcht88xa6l0jufgwx";

	private const string PayjoinAddress =
		"bitcoin:tb1q0382a3m2jzvyk5lkea5h5jcht88xa6l0jufgwx?amount=0.00010727&pj=https://payjoin.test.kukks.org/BTC/pj";

	private const string InvalidPayjoinAddress =
		"bitcoin:tb1q0382a3m2jzvyk5lkea5h5jcht88xa6l0jufgwx?amount=0.00010727&BLABLABLA=https://payjoin.test.kukks.org/BTC/pj";

	[Fact]
	public void Valid_regular_address()
	{
		var sut = Address.FromRegularAddress(BtcAddress, Network.TestNet);
		sut.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Invalid_regular_address()
	{
		var sut = Address.FromRegularAddress("", Network.TestNet);
		sut.IsSuccess.Should().BeFalse();
	}

	[Fact]
	public void Valid_Payjoin_address()
	{
		var sut = Address.FromPayjoin(PayjoinAddress, Network.TestNet);
		sut.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Invalid_Payjoin_address()
	{
		var sut = Address.FromPayjoin(InvalidPayjoinAddress, Network.TestNet);
		sut.IsSuccess.Should().BeFalse();
	}
}
