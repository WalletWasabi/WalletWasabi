namespace Gma.QrCodeNet.Encoding
{
	/// <summary>
	/// Use this exception for null or empty input string or when input string is too large.
	/// </summary>
	public class InputOutOfBoundaryException : System.Exception
	{
		public InputOutOfBoundaryException() : base()
		{
		}

		public InputOutOfBoundaryException(string message) : base(message)
		{
		}
	}
}
