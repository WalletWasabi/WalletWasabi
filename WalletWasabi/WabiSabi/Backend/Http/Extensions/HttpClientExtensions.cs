using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.WabiSabi.Backend.Http.Extensions;

public static class HttpClientExtensions
{
	public static void Configure(this HttpClient me, Uri? baseAddress, string? token, TimeSpan? timeout)
	{
		if (baseAddress is not null)
		{
			me.BaseAddress = baseAddress;
		}

		if (token is not null)
		{
			me.DefaultRequestHeaders.Authorization = new("Bearer", token);
		}

		if (timeout is not null)
		{
			me.Timeout = timeout.Value;
		}
	}
	public static async Task<HttpResponseMessage> SendRequestAsync(this HttpClient me, HttpRequestMessage request, string? statistaName = null, CancellationToken cancel = default)
	{
		var stopWatch = new Stopwatch();

		stopWatch.Start();
		var response = await me.SendAsync(request, cancel).ConfigureAwait(false);
		stopWatch.Stop();

		if (statistaName is { })
		{
			RequestTimeStatista.Instance.Add(statistaName, stopWatch.Elapsed);
		}

		return response;
	}
}