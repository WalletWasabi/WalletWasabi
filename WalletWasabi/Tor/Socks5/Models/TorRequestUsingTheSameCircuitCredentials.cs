using System.Net;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Models;

public class TorRequestUsingTheSameCircuitCredentials : ICredentials
{
	public TorRequestUsingTheSameCircuitCredentials()
	{
		string value = RandomString.CapitalAlphaNumeric(21, secureRandom: true);
		Credentials = new(value, value);
	}

	private NetworkCredential Credentials { get; }

	/// <inheritdoc/>
	public NetworkCredential? GetCredential(Uri uri, string authType)
		=> Credentials;
}
