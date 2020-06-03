using System;

namespace WalletWasabi.Gui.CrashReporter.Models
{
	[Serializable]
	public class SerializedException
	{
		public Type ExceptionType { get; set; }
		public string Message { get; set; }
		public string StackTrace { get; set; }
		public SerializedException InnerException { get; set; }

		public SerializedException()
		{
		}

		public SerializedException(Exception ex)
		{
			if (ex.InnerException is object)
			{
				InnerException = new SerializedException(ex.InnerException);
			}
			ExceptionType = ex.GetType();
			Message = ex.Message;
			StackTrace = ex.StackTrace;
		}
	}
}
