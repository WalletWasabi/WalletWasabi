using System;
using System.Collections.Generic;
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
            var inputs = new[]
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
                $"{host}:5000",
                $" {host}:5000",
                $"{host} :5000",
                $" {host}:5000",
                $"{host}: 5000",
                $" {host} : 5000 ",
                $"{host}/",
                $"{host}/ ",
                $" {host}/ ",
                $"{host}/:5000",
                $"{host}/:5000/",
                $"{host}/:5000/ ",
                $"{host}/: 5000/",
                $"{host}/ :5000/ ",
                $"{host}/ : 5000/",
                $"{host}/ : 5000/ ",
                $"         {host}/              :             5000/           "
            };

            foreach (var inputString in inputs)
            {
                var success = EndPointParser.TryParse(inputString, 5000, out EndPoint ep);
                Assert.True(success);
                var actualPort = ep.GetPortOrDefault();
                Assert.Equal(5000, actualPort);
                var actualHost = ep.GetHostOrDefault();
                var expectedHost = host;
                if (host == "localhost")
                {
                    expectedHost = "127.0.0.1";
                }
                Assert.Equal(expectedHost, actualHost);
                Assert.Equal($"{actualHost}:{actualPort}", ep.ToString(5000));
            }
        }
    }
}
