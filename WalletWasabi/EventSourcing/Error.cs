using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing
{
	public record Error : IError
	{
		public string PropertyName { get; init; } = string.Empty;
		public string ErrorMessage { get; init; }

		public Error(string errorMessage)
		{
			ErrorMessage = errorMessage;
		}

		public Error(string propertyName, string errorMessage) : this(errorMessage)
		{
			PropertyName = propertyName;
		}
	}
}
