using System;
using System.Collections.Generic;

namespace WalletWasabi.Helpers
{
	public class ErrorDescriptors : List<ErrorDescriptor>
	{
		public bool HasErrors => this.Count == 0; 
	}

	public enum ErrorSeverity
	{
		Default,
		Info,
		Warning,
		Error
	}

	public struct ErrorDescriptor : IEquatable<ErrorDescriptor>
	{
		public static ErrorDescriptor Default = new ErrorDescriptor(ErrorSeverity.Default, string.Empty);


		public ErrorSeverity Severity { get; }
		public string Message { get; }

		public ErrorDescriptor(ErrorSeverity severity, string message)
		{
			this.Severity = severity;
			this.Message = message;
		}

		public bool Equals(ErrorDescriptor other)
		{
			return (this.Severity == other.Severity && this.Message == other.Message);
		}
	}
}
