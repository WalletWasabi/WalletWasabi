namespace WalletWasabi.Http.Models
{
	// https://tools.ietf.org/html/rfc7230#section-2.7.3
	// The scheme and host are case-insensitive and normally provided in lowercase;
	// all other components are compared in a case-sensitive manner.
	public enum UriScheme
	{
		http,
		https
	}
}
