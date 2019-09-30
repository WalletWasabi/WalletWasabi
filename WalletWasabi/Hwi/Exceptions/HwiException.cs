using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Hwi.Exceptions
{
	public class HwiException : Exception
	{
		public HwiErrorCode ErrorCode { get; }

		public HwiException(HwiErrorCode errorCode, string message) : base(message)
		{
			ErrorCode = errorCode;
		}
	}
}
