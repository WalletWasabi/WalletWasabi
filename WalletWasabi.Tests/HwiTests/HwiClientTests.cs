using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Hwi2;
using Xunit;

namespace WalletWasabi.Tests.HwiTests
{
	public class HwiClientTests
	{
		[Theory]
		[InlineData("Main")]
		[InlineData("TestNet")]
		[InlineData("RegTest")]
		public void CanCreateHwiClient(string networkString)
		{
			var network = Network.GetNetwork(networkString);
			new HwiClient(network);
		}

		[Fact]
		public void HwiClientConstructorThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(() => new HwiClient(null));
		}
	}
}
