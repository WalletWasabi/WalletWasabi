using System.Net.Http;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests.WabiSabi
{
	public abstract class IntegrationTest : IClassFixture<ArenaApiApplicationFactory>
	{
		protected readonly ArenaApiApplicationFactory Factory;
		protected readonly HttpClient HttpClient;

		public IntegrationTest(ArenaApiApplicationFactory factory)
		{
			Factory = factory;
			HttpClient = Factory.CreateClient();
		}
	}
}
