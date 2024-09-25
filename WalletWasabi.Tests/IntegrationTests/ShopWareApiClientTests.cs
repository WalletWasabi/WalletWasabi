using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using Xunit;
using WalletWasabi.WebClients.Wasabi;
using Uri = System.Uri;

namespace WalletWasabi.Tests.IntegrationTests;

public class ShopWareApiClientTests
{
	[Fact]
	public async Task CanFetchCountriesAndStatesAsync()
	{
		TestSetup testSetup = TestSetup.ForClearnet();
		ShopWareApiClient shopWareApiClient = testSetup.ShopWareApiClient;

		var toSerialize = new List<CachedCountry>();
		var currentPage = 0;
		while (true)
		{
			currentPage++;

			var countryResponse = await shopWareApiClient.GetCountriesAsync("none", ShopWareRequestFactory.GetPage(currentPage, 100), CancellationToken.None);
			var cachedCountries = countryResponse.Elements
				.Where(x => x.Active)
				.Select(x => new CachedCountry(
					Id: x.Id,
					Name: x.Name)
				);

			toSerialize.AddRange(cachedCountries);

			if (countryResponse.Total != countryResponse.Limit)
			{
				break;
			}
		}

		// If a country is added or removed, test will fail and we will be notified.
		// We could go further and verify equality.
		Assert.Equal(246, toSerialize.Count);

		var stateResponse = await shopWareApiClient.GetStatesByCountryIdAsync("none", toSerialize.First(c => c.Name == "United States of America").Id, CancellationToken.None);
		Assert.Equal(51, stateResponse.Elements.Count);

		// Save the new file if it changed
		// var outputFolder = Directory.CreateDirectory(Common.GetWorkDir(nameof(ShopWareApiClient), "ShopWareApiClient"));
		// await File.WriteAllTextAsync(Path.Combine(outputFolder.FullName, "Countries.json"), JsonSerializer.Serialize(toSerialize));
	}

	private class TestSetup
	{
		private TestSetup(bool useTor)
		{
			var apiEndpoint = new Uri("https://shopinbit.solution360.dev/store-api/");
			var httpClientFactory = useTor
				? new OnionHttpClientFactory(new Uri($"socks5://{Common.TorSocks5Endpoint}"))
				: new HttpClientFactory();

			var httpClient = httpClientFactory.CreateClient("carol");
			ShopWareApiClient = new(httpClient, apiEndpoint, "SWSCVTGZRHJOZWF0MTJFTK9ZSG");
		}

		public ShopWareApiClient ShopWareApiClient { get; }

		public static TestSetup ForClearnet()
			=> new(useTor: false);

		public static TestSetup ForTor()
			=> new(useTor: true);
	}
}
