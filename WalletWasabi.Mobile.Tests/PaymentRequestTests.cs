using NBitcoin;
using WalletWasabi.Mobile;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class PaymentRequestTests
{
	private const string Address = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4";

	[Fact]
	public void RoundtripRequest()
	{
		var original = new PaymentRequest(BitcoinAddress.Create(Address, Network.Main), Money.Satoshis(12345), "Alice & Bob", "");
		Assert.Equal(original, PaymentRequest.Parse(original.ToUri(), Network.Main));
	}

	[Theory]
	[InlineData("req-payjoin=1")]
	[InlineData("req-amount=1")]
	[InlineData("amount=1&amount=2")]
	[InlineData("amount=-1")]
	[InlineData("amount=0")]
	[InlineData("amount=0.000000001")]
	[InlineData("amount=21000001")]
	[InlineData("amount=1e2")]
	[InlineData("amount=1,000")]
	public void RejectUnsafeRequests(string query) => Assert.ThrowsAny<Exception>(() => PaymentRequest.Parse($"bitcoin:{Address}?{query}", Network.Main));

	[Fact]
	public void RejectWrongNetwork() => Assert.ThrowsAny<Exception>(() => PaymentRequest.Parse(Address, Network.TestNet4));

	[Theory]
	[InlineData("0.00000001", 1)]
	[InlineData("1.23456789", 123456789)]
	public void PreserveEverySatoshi(string value, long satoshis) => Assert.Equal(satoshis, PaymentRequest.ParseAmount(value).Satoshi);

	[Fact]
	public void BoundScannerInput() => Assert.Throws<FormatException>(() => PaymentRequest.Parse(new string('a', 4097), Network.Main));
}
