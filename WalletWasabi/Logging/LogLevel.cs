namespace WalletWasabi.Logging
{
	public enum LogLevel
	{
		/// <summary>
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		Trace,

		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		Debug,

		/// <summary>
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		Info,

		/// <summary>
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		Warning,

		/// <summary>
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		Error,

		/// <summary>
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		Critical
	}
}
