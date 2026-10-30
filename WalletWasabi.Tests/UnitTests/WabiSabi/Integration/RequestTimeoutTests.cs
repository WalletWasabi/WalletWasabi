using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WalletWasabi.Coordinator.Filters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class RequestTimeoutTests
{
	[Fact]
	public async Task SlowRequestIsCutByTheTimeoutPolicyAsync()
	{
		await using var factory = new WabiSabiApiApplicationFactory<Startup>();
		await using var coordinatorApp = factory.WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services =>
			{
				services.AddControllers().AddApplicationPart(typeof(SlowController).Assembly);
				services.PostConfigure<RequestTimeoutOptions>(options =>
					options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromMilliseconds(200) });
			}));

		using var response = await coordinatorApp.CreateClient().GetAsync("slow");

		Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
	}
}

[ExceptionTranslate]
public class SlowController : ControllerBase
{
	[HttpGet("slow")]
	public async Task GetAsync(CancellationToken cancellationToken)
	{
		// Long enough to answer 200 OK if the policy does not run, short enough to keep the test quick in that case.
		await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
	}
}
