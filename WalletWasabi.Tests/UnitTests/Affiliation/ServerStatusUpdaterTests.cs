using Moq;
using Moq.Language;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Affiliation;
using WalletWasabi.Tor.Http;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class AffiliateServerStatusUpdaterTests
{
	private (Mock<IHttpClient> mockHttpClient, ISetupSequentialResult<Task<HttpResponseMessage>> setup) CreateMockHttpClient(string jsonContent)
	{
		var mockHttpClient = new Mock<IHttpClient>();
		var setup = mockHttpClient.SetupSequence(
			httpClient => httpClient.SendAsync(
				HttpMethod.Post,
				"get_status",
				It.Is<HttpContent>(httpContent => httpContent.ReadAsStringAsync(CancellationToken.None).Result == jsonContent),
				It.IsAny<CancellationToken>()));
		return (mockHttpClient, setup);
	}
	[Fact]
	public async Task GetStatusTestAsync()
	{
		// This test simulates connections with two affiliate servers:
		//
		// | iter # | server-1         | server-2     | result                   |
		// +--------+------------------+--------------+--------------------------+
		// |      1 | Ok               | Ok           | [ server-1; server-2 ]   |
		// |      2 | Ok               | NotFound     | [ server-1 ]             |
		// |      3 | InternalServer   | Forbidden    | []                       |
		// |      4 | Ok               | Ok           | [ server-1; server-2 ]   |
		static HttpResponseMessage Ok()
		{
			HttpResponseMessage response = new(HttpStatusCode.OK);
			response.Content = new StringContent("{}");
			return response;
		}
		static HttpResponseMessage Error(HttpStatusCode statusCode)
		{
			HttpResponseMessage response = new(statusCode);
			response.Content = new StringContent("{}");
			return response;
		}
		using HttpResponseMessage ok10 = Ok();
		using HttpResponseMessage ok11 = Ok();
		using HttpResponseMessage error10 = Error(HttpStatusCode.InternalServerError);
		using HttpResponseMessage ok12 = Ok();
		var (client1Mock, setup1) = CreateMockHttpClient(jsonContent: "{}");
		setup1
			.ReturnsAsync(ok10)
			.ReturnsAsync(ok11)
			.ReturnsAsync(error10)
			.ReturnsAsync(ok12);
		using HttpResponseMessage ok20 = Ok();
		using HttpResponseMessage error20 = Error(HttpStatusCode.NotFound);
		using HttpResponseMessage error21 = Error(HttpStatusCode.Forbidden);
		using HttpResponseMessage ok22 = Ok();
		var (client2Mock, setup2) = CreateMockHttpClient(jsonContent: "{}");
		setup2
			.ReturnsAsync(ok20)
			.ReturnsAsync(error20)
			.ReturnsAsync(error21)
			.ReturnsAsync(ok22);
		Dictionary<string, AffiliateServerHttpApiClient> servers = new()
		{
			["server-1"] = new AffiliateServerHttpApiClient(client1Mock.Object),
			["server-2"] = new AffiliateServerHttpApiClient(client2Mock.Object)
		};
		using AffiliateServerStatusUpdater serverStatusUpdater = new(servers);
		try
		{
			await serverStatusUpdater.StartAsync(CancellationToken.None);
			await Task.Delay(200);

			// Iteration #1.
			Assert.Equal(new[] { "server-1", "server-2" }, serverStatusUpdater.GetRunningAffiliateServers());

			// server-2 failed and is removed.
			await serverStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			// Iteration #2.
			Assert.Equal(new[] { "server-1" }, serverStatusUpdater.GetRunningAffiliateServers());

			// server-1 and server-2 failed and are removed.
			await serverStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			// Iteration #3.
			Assert.Equal(Array.Empty<string>(), serverStatusUpdater.GetRunningAffiliateServers());

			// server-1 and server-2 go back online.
			await serverStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			// Iteration #4.
			Assert.Equal(new[] { "server-1", "server-2" }, serverStatusUpdater.GetRunningAffiliateServers());

			client1Mock.VerifyAll();
			client2Mock.VerifyAll();
		}
		finally
		{
			await serverStatusUpdater.StopAsync(CancellationToken.None);
		}
	}
}
