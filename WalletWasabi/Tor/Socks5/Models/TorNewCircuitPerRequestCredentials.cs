using System.Net;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.Tor.Socks5.Models;

public class TorNewCircuitPerRequestCredentials : ICredentials
{
	public NetworkCredential? GetCredential(Uri uri, string authType)
	{
		string random = RandomString.CapitalAlphaNumeric(21, secureRandom: true);
		return new NetworkCredential(userName: random, password: random);
	}
}
