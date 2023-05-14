using NBitcoin;
using NBitcoin.Payment;
using System.Linq;
using System.Net;
using WalletWasabi.Extensions;
using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

public class ParserTests
{
	[Theory]
	[InlineData("localhost")]
	[InlineData("127.0.0.1")]
	[InlineData("192.168.56.1")]
	[InlineData("foo.com")]
	[InlineData("foo.onion")]
	public void EndPointParserTests(string host)
	{
		var inputsWithoutPorts = new[]
		{
			host,
			$"{host} ",
			$"bitcoin-p2p://{host}",
			$"Bitcoin-P2P://{host}",
			$"tcp://{host}",
			$"TCP://{host}",
			$" {host}",
			$" {host} ",
			$"{host}:",
			$"{host}: ",
			$"{host} :",
			$"{host} : ",
			$" {host} : ",
			$"{host}/",
			$"{host}/ ",
			$" {host}/ ",
		};

		var inputsWithPorts = new[]
		{
			$"{host}:5000",
			$"bitcoin-p2p://{host}:5000",
			$"BITCOIN-P2P://{host}:5000",
			$"tcp://{host}:5000",
			$"TCP://{host}:5000",
			$" {host}:5000",
			$"{host} :5000",
			$" {host}:5000",
			$"{host}: 5000",
			$" {host} : 5000 ",
			$"{host}/:5000",
			$"{host}/:5000/",
			$"{host}/:5000/ ",
			$"{host}/: 5000/",
			$"{host}/ :5000/ ",
			$"{host}/ : 5000/",
			$"{host}/ : 5000/ ",
			$"         {host}/              :             5000/           "
		};

		var invalidPortStrings = new[]
		{
			"-1",
			"-5000",
			"999999999999999999999",
			"foo",
			"-999999999999999999999",
			int.MaxValue.ToString(),
			uint.MaxValue.ToString(),
			long.MaxValue.ToString(),
			"0.1",
			int.MinValue.ToString(),
			long.MinValue.ToString(),
			(ushort.MinValue - 1).ToString(),
			(ushort.MaxValue + 1).ToString()
		};

		var validPorts = new[]
		{
			0,
			5000,
			9999,
			ushort.MinValue,
			ushort.MaxValue
		};

		var inputsWithInvalidPorts = invalidPortStrings.Select(x => $"{host}:{x}").ToArray();

		// Default port is used.
		foreach (var inputString in inputsWithoutPorts)
		{
			foreach (var defaultPort in validPorts)
			{
				if (EndPointParser.TryParse(inputString, defaultPort, out EndPoint? ep))
				{
					AssertEndPointParserOutputs(ep, host, defaultPort);
				}
				else
				{
					Assert.True(false, "Parsing failed.");
				}
			}
		}

		// Default port is not used.
		foreach (var inputString in inputsWithPorts)
		{
			if (EndPointParser.TryParse(inputString, 12345, out EndPoint? ep))
			{
				AssertEndPointParserOutputs(ep, host, 5000);
			}
			else
			{
				Assert.True(false, "Parsing failed.");
			}
		}

		// Default port is invalid, string port is not provided.
		foreach (var inputString in inputsWithoutPorts)
		{
			Assert.False(EndPointParser.TryParse(inputString, -1, out _));
		}

		// Default port doesn't correct invalid port input.
		foreach (var inputString in inputsWithInvalidPorts)
		{
			foreach (var defaultPort in validPorts)
			{
				Assert.False(EndPointParser.TryParse(inputString, defaultPort, out _));
			}
		}

		// Both default and string ports are invalid.
		foreach (var inputString in inputsWithInvalidPorts)
		{
			Assert.False(EndPointParser.TryParse(inputString, -1, out _));
		}
	}

	private static void AssertEndPointParserOutputs(EndPoint endPoint, string expectedHost, int expectedPort)
	{
		Assert.True(endPoint.TryGetHostAndPort(out string? actualHost, out int? actualPort));

		expectedHost = expectedHost == "localhost" ? "127.0.0.1" : expectedHost;

		Assert.Equal(expectedHost, actualHost);
		Assert.Equal(expectedPort, actualPort);

		Assert.Equal($"{actualHost}:{actualPort}", endPoint.ToString(expectedPort));
	}

	[Fact]
	public void BitcoinAddressParserTests()
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

		foreach ((string address, Network network) in tests)
		{
			Assert.False(AddressStringParser.TryParseBitcoinAddress(address.Remove(0, 1), network, out string? errorMessage, out _));
			Assert.Equal("Failed to parse a Bitcoin address.", errorMessage);

			Assert.False(AddressStringParser.TryParseBitcoinAddress(address.Remove(5, 1), network, out errorMessage, out _));
			Assert.Equal("Failed to parse a Bitcoin address.", errorMessage);

			Assert.False(AddressStringParser.TryParseBitcoinAddress(address.Insert(4, "b"), network, out errorMessage, out _));
			Assert.Equal("Failed to parse a Bitcoin address.", errorMessage);

			Assert.False(AddressStringParser.TryParseBitcoinAddress(address, null!, out _));
			Assert.False(AddressStringParser.TryParseBitcoinAddress(null!, network, out _));

			Assert.True(AddressStringParser.TryParseBitcoinAddress(address, network, out BitcoinUrlBuilder? result));
			Assert.Equal(address, result!.Address!.ToString());

			Assert.True(AddressStringParser.TryParseBitcoinAddress(address.Insert(0, "   "), network, out result));
			Assert.Equal(address.Trim(), result!.Address!.ToString());
		}
	}

	[Fact]
	public void BitcoinUriParserTests()
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
