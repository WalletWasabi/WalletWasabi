using NBitcoin;
using WalletWasabi.Fluent.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

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
		Assert.True(sut.IsSuccess);
	}

	[Fact]
	public void Invalid_regular_address()
	{
		var sut = Address.FromRegularAddress("", Network.TestNet);
		Assert.False(sut.IsSuccess);
	}

	[Fact]
	public void Valid_Payjoin_address()
	{
		var sut = Address.FromPayjoin(PayjoinAddress, Network.TestNet);
		Assert.True(sut.IsSuccess);
	}

	[Fact]
	public void Invalid_Payjoin_address()
	{
		var sut = Address.FromPayjoin(InvalidPayjoinAddress, Network.TestNet);
		Assert.False(sut.IsSuccess);
	}
}
