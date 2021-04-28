using System.Net.Http;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests.WabiSabi
{
	public abstract class IntegrationTest : IClassFixture<WabiSabiApiApplicationFactory>
	{
		protected readonly WabiSabiApiApplicationFactory Factory;
		protected readonly HttpClient HttpClient;

		public IntegrationTest(WabiSabiApiApplicationFactory factory)
		{
			Factory = factory;
			HttpClient = Factory.CreateClient();
		}
	}
}
