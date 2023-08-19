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
					Assert.Fail("Parsing failed.");
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
				Assert.Fail("Parsing failed.");
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
}
