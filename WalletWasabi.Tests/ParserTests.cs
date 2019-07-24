using NBitcoin;
using NBitcoin.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests
{
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

			var inputsWithtPorts = new[]
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
					var success = EndPointParser.TryParse(inputString, defaultPort, out EndPoint ep);
					AssertEndPointParserOutputs(success, ep, host, defaultPort);
				}
			}

			// Default port is not used.
			foreach (var inputString in inputsWithtPorts)
			{
				var success = EndPointParser.TryParse(inputString, 12345, out EndPoint ep);
				AssertEndPointParserOutputs(success, ep, host, 5000);
			}

			// Default port is invalid, string port is not provided.
			foreach (var inputString in inputsWithoutPorts)
			{
				Assert.False(EndPointParser.TryParse(inputString, -1, out EndPoint ep));
			}

			// Defaultport doesn't correct invalid port input.
			foreach (var inputString in inputsWithInvalidPorts)
			{
				foreach (var defaultPort in validPorts)
				{
					Assert.False(EndPointParser.TryParse(inputString, defaultPort, out EndPoint ep));
				}
			}

			// Both default and string ports are invalid.
			foreach (var inputString in inputsWithInvalidPorts)
			{
				Assert.False(EndPointParser.TryParse(inputString, -1, out EndPoint ep));
			}
		}

		private static void AssertEndPointParserOutputs(bool isSuccess, EndPoint endPoint, string expectedHost, int expectedPort)
		{
			Assert.True(isSuccess);
			var actualPort = endPoint.GetPortOrDefault();
			Assert.Equal(expectedPort, actualPort);
			var actualHost = endPoint.GetHostOrDefault();
			if (expectedHost == "localhost")
			{
				expectedHost = "127.0.0.1";
			}
			Assert.Equal((string)expectedHost, actualHost);
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

			foreach (var test in tests)
			{
				Assert.False(AddressStringParser.TryParseBitcoinAddress(test.address.Remove(0, 1), test.network, out _));
				Assert.False(AddressStringParser.TryParseBitcoinAddress(test.address.Remove(5, 1), test.network, out _));
				Assert.False(AddressStringParser.TryParseBitcoinAddress(test.address.Insert(1, "b"), test.network, out _));

				Assert.True(AddressStringParser.TryParseBitcoinAddress(test.address, test.network, out BitcoinUrlBuilder result));
				Assert.Equal(test.address, result.Address.ToString());

				Assert.True(AddressStringParser.TryParseBitcoinAddress(test.address.Insert(0, "   "), test.network, out result));
				Assert.Equal(test.address.Trim(), result.Address.ToString());
			}
		}

		[Fact]
		public void BitcoinUrlParserTests()
		{
			(string url, Network network)[] tests = new[]
			{
				("bitcoin:18cBEMRxXHqzWWCxZNtU91F5sbUNKhL5PX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main),
				("bitcoin:17VZNX1SN5NtKa8UQFxwQbFeFc3iqRYhem?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main),
				("bitcoin:3EktnHQD7RiAE6uzMj2ZifT9YgRrkSgzQX?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main),
				("bitcoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.Main),
				("bitcoin:mipcBbFg9gMiCh81Kj8tqqdgoZub1ZJRfn?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet),
				("bitcoin:2MzQwSSnBHWHqSAqtTVQ6v47XtaisrJa1Vc?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet),
				("bitcoin:tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx?amount=50&label=Luke-Jr&message=Donation%20for%20project%20xyz", Network.TestNet),
			};

			foreach (var test in tests)
			{
				Assert.False(AddressStringParser.TryParseBitcoinUrl(test.url.Substring(1), test.network, out _));
				Assert.False(AddressStringParser.TryParseBitcoinUrl(test.url.Remove(5, 4), test.network, out _));
				Assert.False(AddressStringParser.TryParseBitcoinUrl(test.url.Insert(1, "b"), test.network, out _));

				Assert.True(AddressStringParser.TryParseBitcoinUrl(test.url, test.network, out BitcoinUrlBuilder result));
				Assert.Equal(test.url.Split(new[] { ':', '?' })[1], result.Address.ToString());

				Assert.True(AddressStringParser.TryParseBitcoinUrl(test.url.Insert(0, "   "), test.network, out result));
				Assert.Equal(test.url.Split(new[] { ':', '?' })[1], result.Address.ToString());
				Assert.Equal("Luke-Jr", result.Label);
				Assert.Equal(Money.Coins(50m), result.Amount);
			}
		}
	}
}
