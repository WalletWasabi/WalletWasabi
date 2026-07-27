using NBitcoin;
using WalletWasabi.Payjoin;
using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

/// <summary>
/// Tests for <see cref="AddressParser"/>.
/// </summary>
public class AddressParserTests
{
	/// <summary>A realistic BIP 77 <c>pj=</c> value.</summary>
	/// <seealso cref="Bip77UriParams"/>
	private const string Bip77PjUrl = "https://payjo.in/TXJCGKTKXLUUZ#EX1WKV8CEC-OH1QYPM59NK2LXXS4890SUAXXYT25Z2VAPHP0X7YEYCJXGWAG6UG9ZU6NQ-RK1Q0DJS3VVDXWQQTLQ8022QGXSX7ML9PHZ6EDSF6AKEWQG758JPS2EV";

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
			Assert.Equal("Invalid Bitcoin address.", AddressParser.Parse(address[1..], network).Error);
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

		// As per BIP321, keys are case insensitive, "Amount" and "Label" are thus valid.
		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?Amount=20.3&Label=Luke-Jr", Network.Main).Value);
		Assert.Equal("18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX", result.Address.ToWif(Network.Main));
		Assert.Equal("Luke-Jr", result.Label);
		Assert.Equal(20.3m, result.Amount);

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

		// BIP321 Uri with silent payment address
		result = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse("bitcoin:sp1qqgste7k9hx0qftg6qmwlkqtwuy6cycyavzmzj85c6qdfhjdpdjtdgqjuexzk6murw56suy3e0rd2cgqvycxttddwsvgxe2usfpxumr70xc9pkqwv?amount=0.02&label=bolt11_example", Network.Main).Value);
		var sp = Assert.IsType<Address.SilentPayment>(result.Address);
		Assert.Equal("sp1qqgste7k9hx0qftg6qmwlkqtwuy6cycyavzmzj85c6qdfhjdpdjtdgqjuexzk6murw56suy3e0rd2cgqvycxttddwsvgxe2usfpxumr70xc9pkqwv", sp.ToWif(Network.Main));
		Assert.Equal("bolt11_example", result.Label);
		Assert.Equal(0.02m, result.Amount);
	}

	/// <summary>
	/// Inside a BIP 21 URI the whole pj value is percent-encoded. <see cref="AddressParser"/> must hand back the decoded URL and fragment intact.
	/// </summary>
	[Fact]
	public void PreservesBip77PjUrlFragmentParameters()
	{
		string encodedPjUrl = Uri.EscapeDataString(Bip77PjUrl);
		string bip21 = $"bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=0.00010727&pj={encodedPjUrl}";

		var result = AddressParser.Parse(bip21, Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Equal(Bip77PjUrl, uri.PayjoinEndpoint);
		Assert.Equal(0.00010727m, uri.Amount);
	}

	/// <summary>
	/// <c>pjos=0</c> (output substitution disabled) must survive parsing so the sender can rebuild a faithful BIP 21.
	/// </summary>
	[Fact]
	public void PreservesPjosParameter()
	{
		string bip21 = $"bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=1&pj={Uri.EscapeDataString(Bip77PjUrl)}&pjos=0";

		var result = AddressParser.Parse(bip21, Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Equal("0", uri.PayjoinOutputSubstitution);

		// And it round-trips through serialization.
		Assert.Contains("pjos=0", uri.ToWif(Network.TestNet));
	}

	[Fact]
	public void Bip77UriParams_DetectsVersionAndExtractsReceiverKey()
	{
		Assert.True(Bip77UriParams.IsBip77(Bip77PjUrl));
		Assert.True(Bip77UriParams.TryGetReceiverKey(Bip77PjUrl, out var receiverKey));
		Assert.Equal("RK1Q0DJS3VVDXWQQTLQ8022QGXSX7ML9PHZ6EDSF6AKEWQG758JPS2EV", receiverKey);

		// A BIP 78 (v1) endpoint has no BIP 77 fragment params.
		Assert.False(Bip77UriParams.IsBip77("https://btcpay.example/BTC/pj"));
		Assert.False(Bip77UriParams.TryGetReceiverKey("https://btcpay.example/BTC/pj", out _));

		// A fragment without the receiver key is not BIP 77.
		Assert.False(Bip77UriParams.IsBip77("https://payjo.in/TXJCGKTKXLUUZ#EX1WKV8CEC"));
	}

	/// <summary>BIP 21 without a <c>pj</c> parameter yields no payjoin endpoint.</summary>
	[Fact]
	public void NoPjParameter_YieldsNullEndpoint()
	{
		var result = AddressParser.Parse("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=1", Network.TestNet).Value;

		var uri = Assert.IsType<Address.Bip21Uri>(result);
		Assert.Null(uri.PayjoinEndpoint);
	}
}
