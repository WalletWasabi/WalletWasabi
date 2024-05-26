using System.Net;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Models;

public class TorDefaultCircuitCredentials : ICredentials
{
	private TorDefaultCircuitCredentials()
	{
		string value = RandomString.CapitalAlphaNumeric(21, secureRandom: true);
		Credentials = new(value, value);
	}

	public static readonly TorDefaultCircuitCredentials Instance = new();

	private NetworkCredential Credentials { get; }

	/// <inheritdoc/>
	public NetworkCredential? GetCredential(Uri uri, string authType)
		=> Credentials;
}
