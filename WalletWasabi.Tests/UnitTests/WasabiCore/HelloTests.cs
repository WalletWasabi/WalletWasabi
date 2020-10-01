using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Core;
using Xunit;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.UnitTests.WasabiCore
{
	public class HelloTests
	{
		public HelloTests()
		{
			var appFactory = new WebApplicationFactory<Startup>();
			Client = appFactory.CreateClient();
		}

		public HttpClient Client { get; }

		[Fact]
		public async Task HelloAsync()
		{
			var response = await Client.GetAsync("api/v" + Constants.CoreMajorVersion + "/Test/hello");
			var responseString = await response.Content.ReadAsStringAsync();
			var expected = "\"World\"";
			Assert.Equal(expected, responseString);
		}
	}
}
