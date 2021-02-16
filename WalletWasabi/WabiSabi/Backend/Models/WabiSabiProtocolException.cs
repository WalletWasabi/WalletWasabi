using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public class WabiSabiProtocolException : Exception
	{
		public WabiSabiProtocolException(WabiSabiProtocolErrorCode errorCode, string? message = null, Exception? innerException = null)
			: base(message, innerException)
		{
			ErrorCode = errorCode;
		}

		public WabiSabiProtocolErrorCode ErrorCode { get; }
	}
}
