using System.Net;
using WalletWasabi.Userfacing;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Userfacing;

public class EndpointParserTests
{
    [Theory]
    [InlineData("192.168.1.1:8080", "192.168.1.1", 8080, typeof(IPEndPoint))]
    [InlineData("[2001:db8::1]:8080", "2001:db8::1", 8080, typeof(IPEndPoint))]
    [InlineData("example.com:443", "example.com", 443, typeof(DnsEndPoint))]
    [InlineData("localhost:5000", "localhost", 5000, typeof(DnsEndPoint))]
    public void ParseEndpoint_WithValidInputAndPort_ReturnsCorrectEndPoint(
        string endpointString, string expectedAddress, int expectedPort, Type expectedType)
    {
        // Act
        EndPoint result = EndPointParser.Parse(endpointString);

        // Assert
        Assert.IsType(expectedType, result);

        if (result is IPEndPoint ipEndpoint)
        {
            Assert.Equal(expectedAddress, ipEndpoint.Address.ToString());
            Assert.Equal(expectedPort, ipEndpoint.Port);
        }
        else if (result is DnsEndPoint dnsEndpoint)
        {
            Assert.Equal(expectedAddress, dnsEndpoint.Host);
            Assert.Equal(expectedPort, dnsEndpoint.Port);
        }
    }

    [Theory]
    [InlineData("192.168.1.1:80", "192.168.1.1", 80, typeof(IPEndPoint))]
    [InlineData("[2001:db8::1]:443", "2001:db8::1", 443, typeof(IPEndPoint))]
    [InlineData("example.com:443", "example.com", 443, typeof(DnsEndPoint))]
    [InlineData("example.com:8080", "example.com", 8080, typeof(DnsEndPoint))]
    public void ParseEndpoint_WithDefaultPort_ReturnsCorrectEndPoint(
        string endpointString, string expectedAddress,
        int expectedPort, Type expectedType)
    {
        // Act
        EndPoint result = EndPointParser.Parse(endpointString);

        // Assert
        Assert.IsType(expectedType, result);

        if (result is IPEndPoint ipEndpoint)
        {
            Assert.Equal(expectedAddress, ipEndpoint.Address.ToString());
            Assert.Equal(expectedPort, ipEndpoint.Port);
        }
        else if (result is DnsEndPoint dnsEndpoint)
        {
            Assert.Equal(expectedAddress, dnsEndpoint.Host);
            Assert.Equal(expectedPort, dnsEndpoint.Port);
        }
    }

    [Fact]
    public void ParseEndpoint_WithEmptyOrNullString_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => EndPointParser.Parse(endpointString: null!));
    }

    [Theory]
    [InlineData("example.com:99999")] // Port out of range
    [InlineData("example.com:port")] // Port should be a number
    [InlineData("example.com:8080:extra")] // Extra colon
    public void ParseEndpoint_WithInvalidFormat_ThrowsFormatException(string endpointString)
    {
        Assert.Throws<FormatException>(() => EndPointParser.Parse(endpointString));
    }

    [Theory]
    [InlineData("example.com:-1")] // Negative port
    [InlineData("example.com:65536")] // Port too large
    public void ParseEndpoint_WithInvalidDefaultPort_ThrowsArgumentOutOfRangeException(
        string endpointString)
    {
        Assert.Throws<FormatException>(() => EndPointParser.Parse(endpointString));
    }
}
