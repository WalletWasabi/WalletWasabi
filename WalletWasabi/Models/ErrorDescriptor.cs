using System;

namespace WalletWasabi.Models
{
	public struct ErrorDescriptor : IEquatable<ErrorDescriptor>
	{
		public ErrorDescriptor(ErrorSeverity severity, string message)
		{
			Severity = severity;
			Message = message;
		}

		public ErrorSeverity Severity { get; }
		public string Message { get; }

		public bool Equals(ErrorDescriptor other)
		{
			return (Severity == other.Severity && Message == other.Message);
		}
	}
}
