namespace WalletWasabi.Models;

public readonly struct ErrorDescriptor : IEquatable<ErrorDescriptor>
{
	public ErrorDescriptor(ErrorSeverity severity, string message)
	{
		Severity = severity;
		Message = message;
	}

	public ErrorSeverity Severity { get; }
	public string Message { get; }

	#region Equality

	public override bool Equals(object? obj) => obj is ErrorDescriptor desc && this == desc;

	public bool Equals(ErrorDescriptor other) => this == other;

	public override int GetHashCode() => (Severity, Message).GetHashCode();

	public static bool operator ==(ErrorDescriptor? x, ErrorDescriptor? y) => (x?.Severity, x?.Message) == (y?.Severity, y?.Message);

	public static bool operator !=(ErrorDescriptor? x, ErrorDescriptor? y) => !(x == y);

	#endregion Equality
}
