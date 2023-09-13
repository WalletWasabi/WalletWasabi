using NBitcoin;
using NBitcoin.Payment;
using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

/// <summary>
/// Tests for <see cref="AddressStringParser"/>.
/// </summary>
public class AddressStringParserTests
{
	[Fact]
	public void TryParseBitcoinAddressTests()
	{
		(string address, Network network)[] tests = new[]
		{
			("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main),
			("17VZNX1SN5NtKa8UQFxwQbFeFc3iqRYhem", Network.Main),
			("3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX", Network.Main),
			("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", Network.Main),
			("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn", Network.TestNet),
			("2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc", Network.TestNet),
			("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx", Network.TestNet),
		};

		foreach (var test in tests)
		{
			Assert.False(AddressStringParser.TryParseBitcoinAddress(test.address.Remove(0, 1), test.network, out _));
			Assert.False(AddressStringParser.TryParseBitcoinAddress(test.address.Remove(5, 1), test.network, out _));
			Assert.False(AddressStringParser.TryParseBitcoinAddress(test.address.Insert(4, "b"), test.network, out _));

			Assert.False(AddressStringParser.TryParseBitcoinAddress(test.address, null!, out _));
			Assert.False(AddressStringParser.TryParseBitcoinAddress(null!, test.network, out _));

			Assert.True(AddressStringParser.TryParseBitcoinAddress(test.address, test.network, out BitcoinUrlBuilder? result));
			Assert.Equal(test.address, result!.Address!.ToString());

			Assert.True(AddressStringParser.TryParseBitcoinAddress(test.address.Insert(0, "   "), test.network, out result));
			Assert.Equal(test.address.Trim(), result!.Address!.ToString());
		}
	}

	[Fact]
	public void TryParseBitcoinUrlTests()
	{
		string[] invalidUriTests = new[]
		{
			null!,
			"",
			"nfdjksnfjkdsnfjkds",
			"18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX",
			"18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=20.3",
			"mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP",
			"mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=20.3",
			"bitcoin:",
			"bitcoin:?amount=20.3",
			"bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=",
			"bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=XYZ",
			"bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100'000",
			"bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100,000",
			"bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=",
			"bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=XYZ",
			"bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=100'000",
			"bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=100,000",
			"bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?req-somethingyoudontunderstand=50&req-somethingelseyoudontget=999",
			"bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?req-somethingyoudontunderstand=50&req-somethingelseyoudontget=999"
		};
		foreach (var test in invalidUriTests)
		{
			Assert.False(AddressStringParser.TryParseBitcoinUrl(test, Network.Main, out _));
			Assert.False(AddressStringParser.TryParseBitcoinUrl(test, Network.TestNet, out _));
		}

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out BitcoinUrlBuilder? result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());
		Assert.True(AddressStringParser.TryParseBitcoinUrl("BITCOIN:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());
		Assert.True(AddressStringParser.TryParseBitcoinUrl("BitCoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main, out result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?label=Luke-Jr", Network.Main, out result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());
		Assert.Equal("Luke-Jr", result.Label);

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=20.3&label=Luke-Jr", Network.Main, out result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(20.3m), result.Amount);

		// As per BIP21, keys are case sensitive, "Amount" and "Label" is not valid, only "amount" and "label" is.
		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?Amount=20.3&Label=Luke-Jr", Network.Main, out result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());
		Assert.Equal(null!, result.Label);
		Assert.Equal(null!, result.Amount);

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result));
		Assert.Equal("3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX", result.Address!.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main, out result));
		Assert.Equal("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", result.Address!.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet, out result));
		Assert.Equal("2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc", result.Address!.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet, out result));
		Assert.Equal("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx", result.Address!.ToString());
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(Money.Coins(50m), result.Amount);

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?somethingyoudontunderstand=50&somethingelseyoudontget=999", Network.Main, out result));
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address!.ToString());

		Assert.True(AddressStringParser.TryParseBitcoinUrl("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=0.02&label=bolt11_example&lightning=lntb20m1pvjluezsp5zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zygshp58yjmdan79s6qqdhdzgynm4zwqd5d7xmw5fk98klysy043l2ahrqspp5qqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqypqfpp3x9et2e20v6pu37c5d9vax37wxq72un989qrsgqdj545axuxtnfemtpwkc45hx9d2ft7x04mt8q7y6t0k2dge9e7h8kpy9p34ytyslj3yu569aalz2xdk8xkd7ltxqld94u8h2esmsmacgpghe9k8", Network.TestNet, out result));
		Assert.Equal("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP", result.Address!.ToString());
		Assert.Equal("bolt11_example", result.Label);
		Assert.Equal(Money.Coins(0.02m), result.Amount);
	}
}
