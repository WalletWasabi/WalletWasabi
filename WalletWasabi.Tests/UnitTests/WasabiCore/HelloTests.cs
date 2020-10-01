using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Core;
using Xunit;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.RegressionTests;

namespace WalletWasabi.Tests.UnitTests.WasabiCore
{
	public class HelloTests
	{
		[Fact]
		public async Task HelloAsync()
		{
			using var appFactory = new WebApplicationFactory<Startup>()
				.WithWebHostBuilder(builder => builder.UseSetting("datadir", Global.GetWorkDir()));

			using var client = appFactory.CreateClient();
			var response = await client.GetAsync("api/v" + Constants.CoreMajorVersion + "/Test/hello");
			var responseString = await response.Content.ReadAsStringAsync();
			var expected = "\"World\"";
			Assert.Equal(expected, responseString);
		}
	}
}
