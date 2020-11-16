using System;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	/// <summary>
	/// SOCKS5 <see cref="CmdField.Connect"/> command failed to be send.
	/// </summary>
	public class TorHttpException : TorException
	{
		public TorHttpException(string message) : base(message)
		{
		}

		public TorHttpException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
