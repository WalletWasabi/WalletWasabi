using System;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;

namespace WalletWasabi.Exceptions
{
	public class TorSocks5FailureResponseException : Exception
	{
		internal RepField _repField;

		public TorSocks5FailureResponseException(RepField rep) : base($"Tor SOCKS5 proxy responded with {rep}.")
		{
			_repField = Guard.NotNull(nameof(rep), rep);
		}
	}
}
