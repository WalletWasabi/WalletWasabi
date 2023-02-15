using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Language;
using WalletWasabi.Affiliation;
using WalletWasabi.Tor.Http;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class ServerStatusUpdaterTests
{
	public (Mock<IHttpClient> mockHttpClient, ISetupSequentialResult<Task<HttpResponseMessage>> setup) CreateMockHttpClient(string jsonContent)
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
			HttpResponseMessage okResponse = new(HttpStatusCode.OK);
			okResponse.Content = new StringContent("{}");
			return okResponse;
		}
		static HttpResponseMessage Error(HttpStatusCode statusCode)
		{
			HttpResponseMessage okResponse = new(statusCode);
			okResponse.Content = new StringContent("{}");
			return okResponse;
		}

		using HttpResponseMessage ok10 = Ok();
		using HttpResponseMessage ok11 = Ok();
		using HttpResponseMessage error10 = Error(HttpStatusCode.InternalServerError);
		using HttpResponseMessage ok12 = Ok();
		var (client1Mock, setup1) = CreateMockHttpClient("{}");
		setup1
			.ReturnsAsync(ok10)
			.ReturnsAsync(ok11)
			.ReturnsAsync(error10)
			.ReturnsAsync(ok12);

		using HttpResponseMessage ok20 = Ok();
		using HttpResponseMessage error20 = Error(HttpStatusCode.NotFound);
		using HttpResponseMessage error21 = Error(HttpStatusCode.Forbidden);
		using HttpResponseMessage ok22 = Ok();
		var (client2Mock, setup2) = CreateMockHttpClient("{}");
		setup2
			.ReturnsAsync(ok20)
			.ReturnsAsync(error20)
			.ReturnsAsync(error21)
			.ReturnsAsync(ok22);

		Dictionary<string, AffiliateServerHttpApiClient> servers = new ()
		{
			["server-1"] = new AffiliateServerHttpApiClient(client1Mock.Object),
			["server-2"] = new AffiliateServerHttpApiClient(client2Mock.Object)
		};
		using AffiliateServerStatusUpdater serverStatusUpdater = new(servers);
		try
		{
			await serverStatusUpdater.StartAsync(CancellationToken.None);

			var runningServersFirstIter = serverStatusUpdater.GetRunningAffiliateServers();
			Assert.Equal(new[] { "server-1", "server-2" }, runningServersFirstIter);

			// server-2 fails and is removed
			await serverStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			var runningServersSecondIter = serverStatusUpdater.GetRunningAffiliateServers();
			Assert.Equal(new[] { "server-1" }, runningServersSecondIter);

			// server-1 and server-2 fail and are removed
			await serverStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			var runningServersThirdIter = serverStatusUpdater.GetRunningAffiliateServers();
			Assert.Equal(Array.Empty<string>(), runningServersThirdIter);

			// server-1 and server-2 go back online
			await serverStatusUpdater.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			var runningServersFourthIter = serverStatusUpdater.GetRunningAffiliateServers();
			Assert.Equal(new[] { "server-1", "server-2" }, runningServersFourthIter);

			client1Mock.VerifyAll();
			client2Mock.VerifyAll();
		}
		finally
		{
			await serverStatusUpdater.StopAsync(CancellationToken.None);
		}
	}
}
