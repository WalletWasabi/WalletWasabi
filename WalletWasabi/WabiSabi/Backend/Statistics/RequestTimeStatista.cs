using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class RequestTimeStatista
{
	private static readonly Lazy<RequestTimeStatista> Lazy = new(() => new RequestTimeStatista());

	private RequestTimeStatista()
	{
	}

	public static RequestTimeStatista Instance => Lazy.Value;

	private Dictionary<string, List<TimeSpanData>> TimeSpanDataList { get; } = [];
	private Dictionary<string, List<IntegerData>> IntegerDataList { get; } = [];
	private object TimeSpanDataLock { get; } = new();
	private object IntegerDataLock { get; } = new();
	private DateTimeOffset LastDisplayed { get; set; } = DateTimeOffset.UtcNow;
	private TimeSpan DisplayFrequency { get; } = TimeSpan.FromMinutes(60);

	public void Add(string request, TimeSpan duration)
	{
		try
		{
			lock (TimeSpanDataLock)
			{
				TimeSpanData newItem = new(DateTimeOffset.UtcNow, duration);

				if (TimeSpanDataList.TryGetValue(request, out List<TimeSpanData>? value))
				{
					value.Add(newItem);
				}
				else
				{
					TimeSpanDataList.Add(request, [newItem]);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public void Add(string request, int integer)
	{
		try
		{
			lock (IntegerDataLock)
			{
				IntegerData newItem = new(DateTimeOffset.UtcNow, integer);

				if (IntegerDataList.TryGetValue(request, out List<IntegerData>? value))
				{
					value.Add(newItem);
				}
				else
				{
					IntegerDataList.Add(request, [newItem]);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public void FlushStatisticsToLogsIfTimeElapsed()
	{
		if (DateTimeOffset.UtcNow - LastDisplayed < DisplayFrequency)
		{
			return;
		}

		lock (TimeSpanDataLock)
		{
			lock (IntegerDataLock)
			{
				Logger.LogInfo($"Response times for the last {(int)(DateTimeOffset.UtcNow - LastDisplayed).TotalMinutes} minutes:");
				foreach (var request in TimeSpanDataList.OrderByDescending(x => x.Value.Count))
				{
					var seconds = request.Value.Select(x => x.Duration.TotalSeconds);
					Logger.LogInfo($"Responded to '{request.Key}'\t {request.Value.Count} times. Median: {seconds.Median():0.000}s Average: {seconds.Average():0.000}s StdDev: {seconds.StdDev():0.000} Largest {seconds.Max():0.000}s.");
				}

				Logger.LogInfo($"Integer statistics:");
				foreach (var request in IntegerDataList.OrderByDescending(x => x.Value.Count))
				{
					var integers = request.Value.Select(x => x.Integer);
					Logger.LogInfo($"Responded to '{request.Key}'\t {request.Value.Count} times. Minimum: {integers.Min()} Average: {integers.Average():0.0} Maximum: {integers.Max()}.");
				}

				LastDisplayed = DateTimeOffset.UtcNow;
				TimeSpanDataList.Clear();
				IntegerDataList.Clear();
			}
		}
	}

	private record TimeSpanData(DateTimeOffset Time, TimeSpan Duration);
	private record IntegerData(DateTimeOffset Time, int Integer);
}
