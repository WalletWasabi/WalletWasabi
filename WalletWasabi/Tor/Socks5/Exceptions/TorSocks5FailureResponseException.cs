using System;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	public class TorSocks5FailureResponseException : Exception
	{
		public TorSocks5FailureResponseException(RepField rep) : base($"Tor SOCKS5 proxy responded with {rep}.")
		{
			RepField = Guard.NotNull(nameof(rep), rep);
		}

		public RepField RepField { get; }
	}
}
