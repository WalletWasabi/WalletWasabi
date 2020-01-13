using NBitcoin;
using NBitcoin.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class LoggerTests
	{
		[Theory]
		[InlineData("./Program.cs")]
		[InlineData("\\Program.cs")]
		[InlineData("Program.cs")]
		[InlineData("C:\\User\\user\\Github\\WalletWasabi\\WalletWasabi.Gui\\Program.cs")]
		[InlineData("/mnt/C/User/user/Github/WalletWasabi/WalletWasabi.Gui/Program.cs")]
		[InlineData("~/Github/WalletWasabi/WalletWasabi.Gui/Program.cs")]
		[InlineData("Program")]
		public void EndPointParserTests(string path)
		{
			var sourceFileName = EnvironmentHelpers.ExtractFileName(path);
			Assert.Equal("Program", sourceFileName);
		}
	}
}
