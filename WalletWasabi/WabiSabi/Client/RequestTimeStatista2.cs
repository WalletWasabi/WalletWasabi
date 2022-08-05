using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class RequestTimeStatista2
{
	private static readonly Lazy<RequestTimeStatista2> Lazy = new(() => new RequestTimeStatista2());

	private RequestTimeStatista2()
	{
	}

	public static RequestTimeStatista2 Instance => Lazy.Value;

	private Dictionary<string, List<(DateTimeOffset Time, TimeSpan Duration, float SuccessRatio)>> Requests { get; } = new();
	private object Lock { get; } = new();
	private DateTimeOffset LastDisplayed { get; set; } = DateTimeOffset.UtcNow;
	private TimeSpan DisplayFrequency { get; } = TimeSpan.FromMinutes(10);
	private DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;

	public void Add(string request, TimeSpan duration, float successRatio)
	{
		try
		{
			var toDisplay = false;
			lock (Lock)
			{
				if (Requests.ContainsKey(request))
				{
					Requests[request].Add((DateTimeOffset.UtcNow, duration, successRatio));
				}
				else
				{
					Requests.Add(request, new() { (DateTimeOffset.UtcNow, duration, successRatio) });
				}

				if (DateTimeOffset.UtcNow - LastDisplayed > DisplayFrequency)
				{
					toDisplay = true;
				}
			}

			if (toDisplay)
			{
				Display();
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private void Display()
	{
		lock (Lock)
		{
			Logger.LogInfo($"Response times for the last {(int)(DateTimeOffset.UtcNow - Started).TotalMinutes} minutes:");
			foreach (var request in Requests.OrderByDescending(x => x.Value.Count))
			{
				var seconds = request.Value.Select(x => x.Duration.TotalSeconds);

				var fatalFailures = request.Value.Count(x => x.SuccessRatio == 0);
				var successfulRequests = request.Value.Where(x => x.SuccessRatio != 0);
				var successRatio = successfulRequests.Any()
					? successfulRequests.Average(x => x.SuccessRatio)
					: -1;

				Logger.LogInfo($"Responded to '{request.Key}'\t {request.Value.Count} times. Median: {seconds.Median():0.0}s Average: {seconds.Average():0.0}s Largest {seconds.Max():0.0}s SuccessRatio: {successRatio:0.##} FatalFails: {fatalFailures}");
			}

			LastDisplayed = DateTimeOffset.UtcNow;
		}
	}
}
