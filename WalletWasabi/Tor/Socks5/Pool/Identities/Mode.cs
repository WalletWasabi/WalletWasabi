namespace WalletWasabi.Tor.Socks5.Pool.Identities
{
	public enum Mode
	{
		/// <remarks>Corresponds to old <c>isolateStream=false</c>.</remarks>
		DefaultIdentity,

		/// <remarks>Corresponds to old <c>isolateStream=true</c>.</remarks>
		NewIdentityPerRequest,

		SingleIdentityPerLifetime
	}
}
