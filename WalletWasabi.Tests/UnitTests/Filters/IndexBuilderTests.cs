using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.BlockFilters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Filters
{
	public class IndexBuilderTests
	{
		[Fact]
		public void DummyFilterMatchesToFalse()
		{
			var rnd = new Random(123456);
			var blockHash = new byte[32];
			rnd.NextBytes(blockHash);

			var filter = IndexBuilderService.CreateDummyEmptyFilter(new uint256(blockHash));

			var scriptPubKeys = Enumerable.Range(0, 1000).Select(x =>
			{
				var buffer = new byte[20];
				rnd.NextBytes(buffer);
				return buffer;
			});
			var key = blockHash[0..16];
			Assert.False(filter.MatchAny(scriptPubKeys, key));
			Assert.True(filter.MatchAny(IndexBuilderService.DummyScript, key));
		}
	}
}
