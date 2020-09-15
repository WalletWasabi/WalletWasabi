using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class PreventSleepTests
	{
		[Fact]
		public void PreventSleep()
		{
			EnvironmentHelpers.KeepSystemAwake();
		}
	}
}
