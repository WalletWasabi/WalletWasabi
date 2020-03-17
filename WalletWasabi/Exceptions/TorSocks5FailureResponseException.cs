using System;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;

namespace WalletWasabi.Exceptions
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
