using System;

namespace WalletWasabi.Models
{
	public struct ErrorDescriptor : IEquatable<ErrorDescriptor>
	{
		public static ErrorDescriptor Default = new ErrorDescriptor(ErrorSeverity.Default, string.Empty);
		public ErrorSeverity Severity { get; }
		public string Message { get; }

		public ErrorDescriptor(ErrorSeverity severity, string message)
		{
			Severity = severity;
			Message = message;
		}

		public bool Equals(ErrorDescriptor other)
		{
			return (Severity == other.Severity && Message == other.Message);
		}
	}
}
