using WalletWasabi.Extensions;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi
{
	public class HwiCommandsTests
	{
		[Fact]
		public void TestCommandNameAsync()
		{
			Assert.Equal("getmasterxpub", HwiCommands.GetMasterXpub.ToCommandName());
			Assert.Equal("sendpin", HwiCommands.SendPin.ToCommandName());
		}
	}
}
