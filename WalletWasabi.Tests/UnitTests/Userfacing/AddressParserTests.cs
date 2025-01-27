using NBitcoin;
using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

/// <summary>
/// Tests for <see cref="AddressParser"/>.
/// </summary>
public class AddressParserTests
{
	[Fact]
	public void TryParse_BitcoinAddressTests()
	{
		(string address, Network network)[] tests =
		[
			("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main),
			("17VZNX1SN5NtKa8UQFxwQbFeFc3iqRYhem", Network.Main),
			("3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX", Network.Main),
			("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", Network.Main),
			("mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn", Network.TestNet),
			("2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc", Network.TestNet),
			("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx", Network.TestNet)
		];

		foreach ((string address, Network network) in tests)
		{
			Assert.Equal("Invalid Bitcoin address.", AddressParser.Parse(address.Remove(0, 1), network).Error);
			Assert.Equal("Invalid Bitcoin address.", AddressParser.Parse(address.Remove(5, 1), network).Error);
			Assert.Equal("Invalid Bitcoin address.", AddressParser.Parse(address.Insert(4, "b"), network).Error);
			Assert.Equal(
				Assert.IsAssignableFrom<Address>(AddressParser.Parse("  " + address, network).Value),
				Assert.IsAssignableFrom<Address>(AddressParser.Parse(address + "  ", network).Value));

			var parsingResult = AddressParser.Parse(address, network).Value;
			var parsedBitcoinAddress = Assert.IsType<Address.Bitcoin>(parsingResult);
			Assert.Equal(address, parsedBitcoinAddress.ToWif(network));
		}
	}

	[Fact]
	public void TryParse_BitcoinUriTests()
	{
		// Error cases.
		Assert.Equal("Input length is invalid.", AddressParser.Parse("", Network.Main).Error);
		Assert.Equal("Invalid Bitcoin address.", AddressParser.Parse("nfdjksnfjkdsnfjkds", Network.Main).Error);
		Assert.Equal("Bitcoin address is missing.", AddressParser.Parse("bitcoin:", Network.Main).Error);
		Assert.Equal("Bitcoin address is missing.", AddressParser.Parse("bitcoin:?amount=20.3", Network.Main).Error);

		Assert.Equal("Missing amount value.", AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=", Network.Main).Error);

		Assert.Equal("Invalid amount value.", AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=XYZ", Network.Main).Error);
		Assert.Equal("Invalid amount value.", AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100'000", Network.Main).Error);
		Assert.Equal("Invalid amount value.", AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=100,000", Network.Main).Error);

		Assert.Equal("Unsupported required parameter found.", AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?req-somethingyoudontunderstand=50&req-somethingelseyoudontget=999", Network.Main).Error);


		// Success cases.
		var result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("BITCOIN:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("BitCoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?label=Luke-Jr", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=20.3&label=Luke-Jr", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(20.3m, result.Amount);

		// As per BIP21, keys are case sensitive, "Amount" and "Label" is not valid, only "amount" and "label" is.
		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?Amount=20.3&Label=Luke-Jr", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal(null!, result.Label);
		Assert.Equal(null!, result.Amount);

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(50m, result.Amount);

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main).Value);
		Assert.Equal("3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(50m, result.Amount);

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main).Value);
		Assert.Equal("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(50m, result.Amount);

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet).Value);
		Assert.Equal("2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(50m, result.Amount);

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet).Value);
		Assert.Equal("tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(50m, result.Amount);

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?somethingyoudontunderstand=50&somethingelseyoudontget=999", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));

		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP?amount=0.02&label=bolt11_example&lightning=lntb20m1pvjluezsp5zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zyg3zygshp58yjmdan79s6qqdhdzgynm4zwqd5d7xmw5fk98klysy043l2ahrqspp5qqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqqqsyqcyq5rqwzqfqypqfpp3x9et2e20v6pu37c5d9vax37wxq72un989qrsgqdj545axuxtnfemtpwkc45hx9d2ft7x04mt8q7y6t0k2dge9e7h8kpy9p34ytyslj3yu569aalz2xdk8xkd7ltxqld94u8h2esmsmacgpghe9k8", Network.TestNet).Value);
		Assert.Equal("mk2QpYatsKicvFVuTAQLBryyccRXMUaGHP", result.Address.ToWif(Network.Main));
		Assert.Equal("bolt11_example", result.Label);
		Assert.Equal(0.02m, result.Amount);

		// Bip21 Uri with silent payment address}
		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:sp1qqgste7k9hx0qftg6qmwlkqtwuy6cycyavzmzj85c6qdfhjdpdjtdgqjuexzk6murw56suy3e0rd2cgqvycxttddwsvgxe2usfpxumr70xc9pkqwv?amount=0.02&label=bolt11_example", Network.Main).Value);
		var sp = Assert.IsType<Address.SilentPayment>(result.Address);
		Assert.Equal("sp1qqgste7k9hx0qftg6qmwlkqtwuy6cycyavzmzj85c6qdfhjdpdjtdgqjuexzk6murw56suy3e0rd2cgqvycxttddwsvgxe2usfpxumr70xc9pkqwv", sp.ToWif(Network.Main));
		Assert.Equal("bolt11_example", result.Label);
		Assert.Equal(0.02m, result.Amount);
	}
}
