namespace WalletWasabi.Bases;

/// <summary>
/// Tracker that stores the latest received exception, and increases a counter as long as the same exception type is received.
/// </summary>
public class LastExceptionTracker
{
	public ExceptionInfo? LastException { get; set; }

	/// <summary>
	/// Process encountered exception and return the latest exception info.
	/// </summary>
	/// <returns>The latest exception.</returns>
	public ExceptionInfo Process(Exception currentException)
	{
		// Only log one type of exception once.
		if (LastException is { Exception: { } exception }
			&& currentException.GetType() == exception.GetType()
			&& currentException.Message == exception.Message)
		{
			// Increment the counter.
			LastException.ExceptionCount++;
		}
		else
		{
			LastException = new ExceptionInfo(currentException);
		}

		return LastException!;
	}

	/// <summary>
	/// Forget the latest exception.
	/// </summary>
	public void Reset()
	{
		LastException = null;
	}
}
