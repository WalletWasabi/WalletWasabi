using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Exceptions
{
	/// <summary>
	/// SOCKS5 <see cref="CmdField.Connect"/> command failed with an error.
	/// </summary>
	public class TorHttpResponseException : TorHttpException
	{
		public TorHttpResponseException(RepField rep) : base($"Tor SOCKS5 proxy responded with {rep}.")
		{
			RepField = Guard.NotNull(nameof(rep), rep);
		}

		public RepField RepField { get; }
	}
}
