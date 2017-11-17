using System;
using System.Runtime.Serialization;

namespace HiddenWallet.ChaumianCoinJoin
{
	[Serializable]
	internal class TorException : Exception
	{
		public TorException()
		{
		}

		public TorException(string message) : base(message)
		{
		}

		public TorException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected TorException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}