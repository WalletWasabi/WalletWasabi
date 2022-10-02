using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Statistics;

public class StatsLogger
{
	/// <param name="filePath">Absolute path to a CSV file.</param>
	/// <param name="eventPrefix">Common event prefix for all logged events.</param>
	/// <remarks>
	/// <paramref name="eventPrefix"/> can correspond to a namespace or some logging category.
	/// For example, <c>UI.ResponseTimes.PlayBox.{OPERATION=Play,Pause}</c> to log play box operation times.
	/// For example, <c>Networking.HTTP.Durations.CJ.{OPEARTION=GetStatus,RegisterInput}</c> to log CJ API request durations.
	/// </remarks>
	private StatsLogger(string filePath, bool logHeader, params string[] eventPrefix)
	{
		FilePath = filePath;
		EventPrefix = eventPrefix;
		EventAttributesString = string.Join('.', eventPrefix);

		if (logHeader)
		{
			File.AppendAllText(FilePath, "DateTime,Action,DurationMs,TotalDurationMs\n");
		}
	}

	public string FilePath { get; }

	private string[] EventPrefix { get; }

	/// <remarks>Allows us to avoid repeated allocations.</remarks>
	private string EventAttributesString { get; }

	private object Lock { get; } = new();

	/// <remarks>Constructs a new instance with <c>StatsLogger.csv</c> in a current user's temporary folder.</remarks>
	public static StatsLogger From(params string[] eventPrefix)
	{
		return new(Path.Join(Path.GetTempPath(), "StatsLogger.csv"), logHeader: true, eventPrefix);
	}

	public StatsLogger AddScope(params string[] subNamespaceParts)
	{
		return new StatsLogger(FilePath, logHeader: false, EventPrefix.Concat(subNamespaceParts).ToArray());
	}

	public void Log(string eventName, DateTimeOffset actionStart, DateTimeOffset? processStart = null)
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		TimeSpan duration = utcNow - actionStart;
		TimeSpan? totalDuration = (processStart is not null) ? (utcNow - processStart) : null;

		Log(eventName, duration, totalDuration);
	}

	public void Log(string eventName, TimeSpan duration, TimeSpan? totalDuration = null)
	{
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;

		string eventDescription = EventPrefix.Length == 0 ? eventName : $"{EventAttributesString}.{eventName}";

		StringBuilder sb = new();
		sb.Append($"{utcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}");
		sb.Append(',');
		sb.Append(eventDescription);
		sb.Append(',');
		sb.Append((long)duration.TotalMilliseconds);

		if (totalDuration is not null)
		{
			sb.Append($",{(long)totalDuration.Value.TotalMilliseconds}");
		}

		lock (Lock)
		{
			try
			{
				File.AppendAllText(FilePath, sb.ToString() + "\n");
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}
