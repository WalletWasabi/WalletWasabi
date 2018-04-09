using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.TorSocks5.Models.Fields.OctetFields;
using System;

namespace MagicalCryptoWallet.Exceptions
{
	public class TorSocks5FailureResponseException : Exception
	{
		public RepField RepField;
		public TorSocks5FailureResponseException(RepField rep) : base($"Tor SOCKS5 proxy responded with {rep.ToString()}.")
		{
			RepField = Guard.NotNull(nameof(rep), rep);
		}
	}
}
