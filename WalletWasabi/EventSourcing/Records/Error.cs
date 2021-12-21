using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Helpers;

namespace WalletWasabi.EventSourcing.Records
{
	public record Error : IError
	{
		public string PropertyName { get; init; } = string.Empty;
		public string ErrorMessage { get; init; }
		public Exception? InnerException { get; init; } = null;

		public Error(string errorMessage)
		{
			Guard.NotNull(nameof(errorMessage), errorMessage);
			ErrorMessage = errorMessage;
		}

		public Error(Exception innerException)
			: this((innerException ?? throw new ArgumentNullException(nameof(innerException))).Message)
		{
			InnerException = innerException;
		}

		public Error(string propertyName, string errorMessage) : this(errorMessage)
		{
			Guard.NotNull(nameof(propertyName), propertyName);
			PropertyName = propertyName;
		}

		public Error(string propertyName, Exception innerException) : this(innerException)
		{
			Guard.NotNull(nameof(propertyName), propertyName);
			PropertyName = propertyName;
		}
	}
}
