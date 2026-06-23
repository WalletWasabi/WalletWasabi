using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text;
using System.Threading;
using WalletWasabi.Logging;

namespace WalletWasabi.Observability;

public class MetricManager : IDisposable
{
	public const bool EnableMetricConsoleLogging = false;
	public const bool EnableMetricConsoleSnapshots = true;

	public const string P2pMeterName = "WalletWasabi.P2P";

	private readonly MeterListener _listener;

	private readonly Lock _lock = new();
	private readonly OrderedDictionary<string, decimal> _metrics;

	public MetricManager()
	{
		_metrics = new OrderedDictionary<string, decimal>
		{
			{ Metrics.P2pConnectedCounter, 0m },
			{ Metrics.P2pConnectionAttemptsCounter, 0m },
			{ Metrics.P2pConnectionSuccessCounter, 0m },
			{ Metrics.P2pConnectionHandshakeDurationHistogram, 0m },
			{ Metrics.P2pConnectionTotalDurationHistogram, 0m },
			{ Metrics.P2pMisbehaviorCounter, 0m },
			{ Metrics.P2pMisbehaviorInvalidDataCounter, 0m },
			{ Metrics.P2pMisbehaviorTimeoutBlockDownloadCounter, 0m }
		};

		_listener = new()
		{
			InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name == P2pMeterName)
				{
					listener.EnableMeasurementEvents(instrument);
				}
			}
		};

		// Configure the callbacks to invoke when an Instrument emits a value.
		_listener.SetMeasurementEventCallback<int>(OnMetric);
		_listener.SetMeasurementEventCallback<double>(OnMetric);
		_listener.SetMeasurementEventCallback<decimal>(OnMetric);

		// Start the listener, which invokes OnInstrumentPublished for already-published Instruments.
		_listener.Start();
	}

	public OrderedDictionary<string, decimal> GetMetricsSnapshot()
	{
		lock (_lock)
		{
			return new OrderedDictionary<string, decimal>(_metrics);
		}
	}

	private void OnMetric<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
	{
		string name = instrument.Name;
		var exchange = name.EndsWith(".histogram", StringComparison.Ordinal) ||
			 name.EndsWith(".gauge", StringComparison.Ordinal);

		decimal measurementValue = measurement switch
		{
			int i => i,
			double d => (decimal)d,
			decimal d => d,
			_ => throw new NotSupportedException()
		};

		lock (_lock)
		{
			if (_metrics.TryGetValue(name, out var value))
			{
				_metrics[name] = exchange
					? measurementValue
					: value + measurementValue;
			}
		}

#pragma warning disable CS0162 // Unreachable code detected
		if (EnableMetricConsoleLogging)
		{
			var tagsStr = FormatTags(tags);
			var message = $"{instrument.Name} = {measurement} {tagsStr}";
			Logger.LogInfo(message);
		}
#pragma warning restore CS0162 // Unreachable code detected
	}

	private static string FormatTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
	{
		if (tags.IsEmpty)
		{
			return "";
		}

		var sb = new StringBuilder();
		sb.Append('[');

		for (int i = 0; i < tags.Length; i++)
		{
			var tag = tags[i];
			if (i > 0)
			{
				sb.Append(", ");
			}

			sb.Append(tag.Key).Append('=').Append(tag.Value?.ToString() ?? "null");
		}

		sb.Append(']');
		return sb.ToString();
	}

	public void Dispose()
	{
		_listener.Dispose();
	}
}
