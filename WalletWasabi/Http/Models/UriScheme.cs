namespace WalletWasabi.Http.Models
{
	// https://tools.ietf.org/html/rfc7230#section-2.7.3
	// The scheme and host are case-insensitive and normally provided in lowercase;
	// all other components are compared in a case-sensitive manner.
#pragma warning disable IDE1006 // Naming Styles

	public enum UriScheme
	{
		http,
		https
	}

#pragma warning restore IDE1006 // Naming Styles
}
