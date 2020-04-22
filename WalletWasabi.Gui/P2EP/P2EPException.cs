using System;
using System.Runtime.Serialization;

namespace WalletWasabi.Gui.P2EP
{
	[Serializable]
	internal class P2EPException : Exception
	{
		public P2EPException()
		{
		}

		public P2EPException(string message) : base(message)
		{
		}

		public P2EPException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected P2EPException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}