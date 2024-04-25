using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class RequestTimeStatista
{
	public class Stat<T> where T : INumber<T>
	{
		public int Count { get; set; }

		public T Min { get; set; }
		public T Max { get; set; }
		public T Sum { get; set; }
		public T SumSqr { get; set; }

		public Stat()
		{
			Count = 0;
			SumSqr = Sum = Min = Max = T.Zero;
		}

		public void Clear()
		{
			Count = 0;
			SumSqr = Sum = Min = Max = T.Zero;
		}

		public void Add(T value)
		{
			Sum += value;
			SumSqr += value * value;
			Count++;

			if (Count == 1)
			{
				Min = Max = value;
			}
			else
			{
				if (Min > value) { Min = value; }
				else if (Max < value) { Max = value; }
			}
		}
	}

	private static readonly Lazy<RequestTimeStatista> Lazy = new(() => new RequestTimeStatista());

	private RequestTimeStatista()
	{
	}

	public static RequestTimeStatista Instance => Lazy.Value;

	private Dictionary<string, Stat<long>> LongMeasures { get; } = [];
	private Dictionary<string, Stat<Int128>> TickMeasures { get; } = [];

	private object LongDataLock { get; } = new();
	private object TickDataLock { get; } = new();

	private DateTimeOffset LastDisplayed { get; set; } = DateTimeOffset.UtcNow;
	private TimeSpan DisplayFrequency { get; } = TimeSpan.FromMinutes(60);

	public void Add(string request, long value)
	{
		try
		{
			lock (LongDataLock)
			{
				if (!LongMeasures.TryGetValue(request, out Stat<long>? results))
				{
					LongMeasures.Add(request, results = new());
				}
				results.Add(value);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	public void Add(string request, TimeSpan duration)
	{
		try
		{
			lock (TickDataLock)
			{
				if (!TickMeasures.TryGetValue(request, out Stat<Int128>? results))
				{
					TickMeasures.Add(request, results = new());
				}
				results.Add(duration.Ticks);
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

		lock (TickDataLock)
		{
			lock (LongDataLock)
			{
				Logger.LogInfo($"Response times for the last {(int)(DateTimeOffset.UtcNow - LastDisplayed).TotalMinutes} minutes:");
				foreach (var request in TickMeasures.OrderByDescending(x => x.Value.Count))
				{
					Int128 ticksPerSecond = TimeSpan.TicksPerSecond;
					Stat<Int128> measure = request.Value;
					int count = measure.Count;
					if (count > 0)
					{
						double average = ((double)measure.Sum) / TimeSpan.TicksPerSecond / count;
						double maximum = ((double)measure.Max) / TimeSpan.TicksPerSecond;
						Int128 averageTicks = (Int128)(average * TimeSpan.TicksPerSecond);
						// Sum((x-m)^2) = Sum(x^2 - 2*x*m + m^2) = Sum(x^2) - 2*Sum(x)*m + n*m^2
						Int128 stdsqr = measure.SumSqr - (2 * measure.Sum * averageTicks) + (count * averageTicks * averageTicks);
						double stddev = Math.Sqrt(((double)stdsqr) / count) / TimeSpan.TicksPerSecond;
						Logger.LogInfo($"Responded to {$"'{request.Key}'",-40} {count,7} times. Average: {average:#0.000}s StdDev: {stddev:#0.000} Largest {maximum:#0.000}s.");
					}
				}

				Logger.LogInfo($"Integer statistics:");
				foreach (var request in LongMeasures.OrderByDescending(x => x.Value.Count))
				{
					Stat<long> measure = request.Value;
					int count = measure.Count;
					if (count > 0)
					{
						double average = ((double)measure.Sum) / count;
						long minimum = measure.Min;
						long maximum = measure.Max;
						Logger.LogInfo($"Responded to {$"'{request.Key}'",-40} {count,7} times. Average: {average,8:0.0} Minimum: {minimum,8} Maximum: {maximum,8}.");
					}
				}

				LastDisplayed = DateTimeOffset.UtcNow;
				foreach (var measures in TickMeasures.Values) { measures.Clear(); }
				foreach (var measures in LongMeasures.Values) { measures.Clear(); }
			}
		}
	}

	private record TimeSpanData(DateTimeOffset Time, TimeSpan Duration);
	private record IntegerData(DateTimeOffset Time, int Integer);
}
