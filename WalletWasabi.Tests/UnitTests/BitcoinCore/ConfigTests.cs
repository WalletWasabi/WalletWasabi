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

			var expectedConfig =
@"foo = buz

foo bar = buz quxx

too = 1
foo
bar
#qoo=boo
";

			Assert.Equal(expectedConfig, coreConfig.ToString());

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

			coreConfig.TryAdd(add1);
			coreConfig2.AddOrUpdate(add1);
			coreConfig.TryAdd(add2);
			coreConfig2.AddOrUpdate(add2);
			coreConfig.TryAdd(add3);
			coreConfig2.AddOrUpdate(add3);

			configDic = coreConfig.ToDictionary();
			configDic2 = coreConfig2.ToDictionary();

			Assert.True(configDic.TryGetValue("moo", out string mooValue));
			Assert.True(configDic.TryGetValue("foo", out string fooValue));
			Assert.True(configDic.TryGetValue("too", out string tooValue));
			Assert.Equal("1", mooValue);
			Assert.Equal("buz", fooValue);
			Assert.Equal("1", tooValue);

			Assert.True(configDic2.TryGetValue("moo", out mooValue));
			Assert.True(configDic2.TryGetValue("foo", out fooValue));
			Assert.True(configDic2.TryGetValue("too", out tooValue));
			Assert.Equal("1", mooValue);
			Assert.Equal("bar", fooValue);
			Assert.Equal("0", tooValue);

			expectedConfig =
@"foo = buz

foo bar = buz quxx

too = 1
foo
bar
#qoo=boo
moo = 1
";

			Assert.Equal(expectedConfig, coreConfig.ToString());

			var expectedConfig2 =
@"foo bar = buz quxx

foo
bar
#qoo=boo
moo = 1
foo = bar
too = 0
";
			Assert.Equal(expectedConfig2, coreConfig2.ToString());
		}
	}
}
