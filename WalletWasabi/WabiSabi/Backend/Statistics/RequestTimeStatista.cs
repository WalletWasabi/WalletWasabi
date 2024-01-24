using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class RequestTimeStatista
{
	private static readonly Lazy<RequestTimeStatista> Lazy = new(() => new RequestTimeStatista());

	private RequestTimeStatista()
	{
	}

	public static RequestTimeStatista Instance => Lazy.Value;

	private Dictionary<string, List<(DateTimeOffset Time, TimeSpan Duration)>> Requests { get; } = new();
	private object Lock { get; } = new();
	private DateTimeOffset LastDisplayed { get; set; } = DateTimeOffset.UtcNow;
	private TimeSpan DisplayFrequency { get; } = TimeSpan.FromMinutes(60);

	public void Add(string request, TimeSpan duration)
	{
		try
		{
			var toDisplay = false;
			lock (Lock)
			{
				if (Requests.TryGetValue(request, out List<(DateTimeOffset Time, TimeSpan Duration)>? value))
				{
					value.Add((DateTimeOffset.UtcNow, duration));
				}
				else
				{
					Requests.Add(request, new() { (DateTimeOffset.UtcNow, duration) });
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
			Logger.LogInfo($"Response times for the last {(int)(DateTimeOffset.UtcNow - LastDisplayed).TotalMinutes} minutes:");
			foreach (var request in Requests.OrderByDescending(x => x.Value.Count))
			{
				var seconds = request.Value.Select(x => x.Duration.TotalSeconds);
				Logger.LogInfo($"Responded to '{request.Key}'\t {request.Value.Count} times. Median: {seconds.Median():0.0}s Average: {seconds.Average():0.0}s Largest {seconds.Max():0.0}s");
			}

			LastDisplayed = DateTimeOffset.UtcNow;
			Requests.Clear();
		}
	}
}
