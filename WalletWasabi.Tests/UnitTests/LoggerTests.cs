using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class LoggerTests
	{
		[Theory]
		[InlineData("./Program.cs")]
		[InlineData("\\Program.cs")]
		[InlineData("Program.cs")]
		[InlineData("C:\\User\\user\\Github\\WalletWasabi\\WalletWasabi.Fluent\\Program.cs")]
		[InlineData("/mnt/C/User/user/Github/WalletWasabi/WalletWasabi.Fluent/Program.cs")]
		[InlineData("~/Github/WalletWasabi/WalletWasabi.Fluent/Program.cs")]
		[InlineData("Program")]
		public void EndPointParserTests(string path)
		{
			var sourceFileName = EnvironmentHelpers.ExtractFileName(path);
			Assert.Equal("Program", sourceFileName);
		}
	}
}
