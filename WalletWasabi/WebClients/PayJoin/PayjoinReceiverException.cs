namespace WalletWasabi.WebClients.PayJoin
{
	public class PayjoinReceiverException : PayjoinException
	{
		public PayjoinReceiverException(int httpCode, string errorCode, string message)
			: base(message)
		{
			HttpCode = httpCode;
			ErrorCode = errorCode;
			ErrorMessage = message;
		}

		public int HttpCode { get; }
		public string ErrorCode { get; }
		public string ErrorMessage { get; }

		private static string FormatMessage(in int httpCode, string errorCode, string message)
		{
			return $"{errorCode}: {message} (HTTP: {httpCode})";
		}
	}
}
