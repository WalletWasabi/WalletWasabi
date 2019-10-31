using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.BitcoinCore;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class ConfigTests
	{
		[Fact]
		public void CanParse()
		{
			var testConfig =
@"foo=buz
foo = bar

 foo = bar
foo bar = buz quxx

too =1
foo
bar
#qoo=boo";
			var coreConfig = new CoreConfig();
			coreConfig.TryAdd(testConfig);

			var configDic = coreConfig.ToDictionary();

			Assert.True(configDic.TryGetValue("foo", out string v1));
			Assert.True(configDic.TryGetValue("too", out string v2));
			Assert.False(configDic.TryGetValue("qoo", out _));
			Assert.False(configDic.TryGetValue("bar", out _));
			Assert.True(configDic.TryGetValue("foo bar", out string v3));

			Assert.Equal("buz", v1);
			Assert.Equal("1", v2);
			Assert.Equal("buz quxx", v3);

			var coreConfig2 = new CoreConfig();
			coreConfig2.AddOrUpdate(testConfig);

			var configDic2 = coreConfig2.ToDictionary();

			Assert.True(configDic2.TryGetValue("foo", out string v1_2));
			Assert.True(configDic2.TryGetValue("too", out string v2_2));
			Assert.False(configDic2.TryGetValue("qoo", out _));
			Assert.False(configDic2.TryGetValue("bar", out _));
			Assert.True(configDic2.TryGetValue("foo bar", out string v3_3));

			Assert.Equal("bar", v1_2);
			Assert.Equal("1", v2_2);
			Assert.Equal("buz quxx", v3_3);

			var add1 = "moo=1";
			var add2 = "foo=bar";
			var add3 = "too=0";

			var expectedConfig =
@"foo=bar
foo = bar
 foo = bar
foo bar = buz quxx

foo
#qoo=boo
moo = 1
too = 0";
		}
	}
}
