using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
    }
}
