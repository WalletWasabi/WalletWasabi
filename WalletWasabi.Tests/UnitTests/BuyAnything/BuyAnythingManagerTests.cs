using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.WebClients.BuyAnything;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BuyAnything;

public class BuyAnythingManagerTests
{
	[Fact]
	public async Task BuyAnythingManagerTest()
	{
		var mockedShopwareClient = new MockShopWareApiClient();
		var buyAnythingClient = new BuyAnythingClient(mockedShopwareClient);
		using var buyAnythingManager = new BuyAnythingManager("fake-api-key", TimeSpan.FromMinutes(0), buyAnythingClient);

		var countries = await buyAnythingManager.GetCountriesAsync(CancellationToken.None);
		Assert.Single(countries, x => x.Name == "Argentina");


	}
}
