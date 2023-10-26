using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests;

public static class MockHttpClientExtensions
{
	public static void SetupSequence(this MockHttpClient http, params Func<HttpResponseMessage>[] responses)
	{
		var callCounter = 0;
		http.OnSendAsync = req =>
		{
			var responseFn = responses[callCounter];
			Interlocked.Increment(ref callCounter);
			return Task.FromResult(responseFn());
		};
	}
}
