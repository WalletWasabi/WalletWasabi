using NBitcoin;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using WalletWasabi.BitcoinCore.Configuration;
using WalletWasabi.BitcoinCore.Configuration.Whitening;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class ConfigTranslatorTests
	{
		[Fact]
		public void TryGetRpcUserTests()
		{
			var config = new CoreConfig();
			var translatorMain = new CoreConfigTranslator(config, Network.Main);
			var translatorTest = new CoreConfigTranslator(config, Network.TestNet);
			var translatorReg = new CoreConfigTranslator(config, Network.RegTest);
			Assert.Null(translatorMain.TryGetRpcUser());
			Assert.Null(translatorTest.TryGetRpcUser());
			Assert.Null(translatorReg.TryGetRpcUser());

			config.AddOrUpdate("rpcuser");
			Assert.Null(translatorMain.TryGetRpcUser());
			Assert.Null(translatorTest.TryGetRpcUser());
			Assert.Null(translatorReg.TryGetRpcUser());
			config.AddOrUpdate("rpcuser=foo");
			Assert.Equal("foo", translatorMain.TryGetRpcUser());
			Assert.Null(translatorTest.TryGetRpcUser());
			Assert.Null(translatorReg.TryGetRpcUser());
			config.AddOrUpdate("rpcuser=boo");
			Assert.Equal("boo", translatorMain.TryGetRpcUser());
			Assert.Null(translatorTest.TryGetRpcUser());
			Assert.Null(translatorReg.TryGetRpcUser());
			config.AddOrUpdate("main.rpcuser=ooh");
			Assert.Equal("ooh", translatorMain.TryGetRpcUser());
			Assert.Null(translatorTest.TryGetRpcUser());
			Assert.Null(translatorReg.TryGetRpcUser());
			config.AddOrUpdate("rpcuser=boo");
			Assert.Equal("boo", translatorMain.TryGetRpcUser());
			Assert.Null(translatorTest.TryGetRpcUser());
			Assert.Null(translatorReg.TryGetRpcUser());
			config.AddOrUpdate("test.rpcuser=boo");
			Assert.Equal("boo", translatorMain.TryGetRpcUser());
			Assert.Equal("boo", translatorTest.TryGetRpcUser());
			Assert.Null(translatorReg.TryGetRpcUser());
			config.AddOrUpdate("regtest.rpcuser=boo");
			Assert.Equal("boo", translatorMain.TryGetRpcUser());
			Assert.Equal("boo", translatorTest.TryGetRpcUser());
			Assert.Equal("boo", translatorReg.TryGetRpcUser());
		}

		[Fact]
		public void TryGetRpcPortTests()
		{
			var config = new CoreConfig();
			var translator = new CoreConfigTranslator(config, Network.Main);
			Assert.Null(translator.TryGetRpcPort());

			config.AddOrUpdate("rpcport");
			Assert.Null(translator.TryGetRpcPort());

			config.AddOrUpdate("main.rpcport=1");
			Assert.Equal(1, translator.TryGetRpcPort());

			config.AddOrUpdate("rpcport=2");
			Assert.Equal(2, translator.TryGetRpcPort());

			config.AddOrUpdate("main.rpcport=foo");
			Assert.Null(translator.TryGetRpcPort());
		}

		[Fact]
		public void TryGetWhiteBindTests()
		{
			var config = new CoreConfig();
			var translator = new CoreConfigTranslator(config, Network.Main);
			Assert.Null(translator.TryGetWhiteBind());

			config.AddOrUpdate("whitebind");
			Assert.Null(translator.TryGetWhiteBind());

			config.AddOrUpdate("main.whitebind=127.0.0.1:18444");
			WhiteBind whiteBind = translator.TryGetWhiteBind();
			var ipEndPoint = whiteBind.EndPoint as IPEndPoint;
			Assert.Equal(IPAddress.Loopback, ipEndPoint.Address);
			Assert.Equal(18444, ipEndPoint.Port);
			Assert.Equal(string.Empty, whiteBind.Permissions);

			config.AddOrUpdate("whitebind=127.0.0.1:0");
			whiteBind = translator.TryGetWhiteBind();
			ipEndPoint = whiteBind.EndPoint as IPEndPoint;
			Assert.Equal(IPAddress.Loopback, ipEndPoint.Address);
			Assert.Equal(0, ipEndPoint.Port);
			Assert.Equal(string.Empty, whiteBind.Permissions);

			config.AddOrUpdate("whitebind=127.0.0.1");
			whiteBind = translator.TryGetWhiteBind();
			ipEndPoint = whiteBind.EndPoint as IPEndPoint;
			Assert.Equal(IPAddress.Loopback, ipEndPoint.Address);
			// Default port.
			Assert.Equal(8333, ipEndPoint.Port);
			Assert.Equal(string.Empty, whiteBind.Permissions);

			config.AddOrUpdate("whitebind=foo@127.0.0.1");
			whiteBind = translator.TryGetWhiteBind();
			ipEndPoint = whiteBind.EndPoint as IPEndPoint;
			Assert.Equal(IPAddress.Loopback, ipEndPoint.Address);
			Assert.Equal(8333, ipEndPoint.Port);
			Assert.Equal("foo", whiteBind.Permissions);

			config.AddOrUpdate("whitebind=foo,boo@127.0.0.1");
			whiteBind = translator.TryGetWhiteBind();
			ipEndPoint = whiteBind.EndPoint as IPEndPoint;
			Assert.Equal(IPAddress.Loopback, ipEndPoint.Address);
			Assert.Equal(8333, ipEndPoint.Port);
			Assert.Equal("foo,boo", whiteBind.Permissions);

			config.AddOrUpdate("main.whitebind=@@@");
			Assert.Null(translator.TryGetWhiteBind());
		}
	}
}
