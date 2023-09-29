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

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class AffiliateServerStatusUpdaterTests
{
	private MockIHttpClient CreateIMockHttpClient(string jsonContent, params HttpResponseMessage[] responses)
	{
		var mockHttpClient = new MockIHttpClient();

		var callCounter = 0;
		mockHttpClient.OnSendAsync = req =>
		{
			if (req.Method == HttpMethod.Post && req.RequestUri.LocalPath == "/get_status")
			{
				var response = responses[callCounter];
				callCounter++;
				return Task.FromResult(response);
			}

			return Task.FromException<HttpResponseMessage>(new InvalidOperationException());
		};
		return mockHttpClient;
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

		var client1Mock = CreateIMockHttpClient(
			jsonContent: "{}",
			Ok(),
			Ok(),
			Error(HttpStatusCode.InternalServerError),
			Ok());

		var client2Mock = CreateIMockHttpClient(
			jsonContent: "{}",
			Ok(),
			Error(HttpStatusCode.NotFound),
			Error(HttpStatusCode.Forbidden),
			Ok());

		Dictionary<string, AffiliateServerHttpApiClient> servers = new()
		{
			["server-1"] = new AffiliateServerHttpApiClient(client1Mock),
			["server-2"] = new AffiliateServerHttpApiClient(client2Mock)
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
		}
		finally
		{
			await serverStatusUpdater.StopAsync(CancellationToken.None);
		}
	}
}
