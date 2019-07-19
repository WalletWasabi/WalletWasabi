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

            // Default port is used.
            foreach (var inputString in inputsWithoutPorts)
            {
                var success = EndPointParser.TryParse(inputString, 5000, out EndPoint ep);
                AssertEndPointParserOutputs(success, ep, host, 5000);
            }

            // Default port is not used.
            foreach (var inputString in inputsWithtPorts)
            {
                var success = EndPointParser.TryParse(inputString, 12345, out EndPoint ep);
                AssertEndPointParserOutputs(success, ep, host, 5000);
            }

            // Zero can be used as a discarded port.
            foreach (var inputString in inputsWithoutPorts)
            {
                var success = EndPointParser.TryParse(inputString, 0, out EndPoint ep);
                AssertEndPointParserOutputs(success, ep, host, 0);
            }

            // -1 means default port is not accepted.
            foreach (var inputString in inputsWithoutPorts)
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
