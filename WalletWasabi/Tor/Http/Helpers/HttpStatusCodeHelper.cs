using System.Net;

namespace WalletWasabi.Tor.Http.Helpers;

public static class HttpStatusCodeHelper
{
	/// <summary>
	/// 1xx
	/// </summary>
	public static bool IsInformational(HttpStatusCode status)
	{
		return (int)status >= 100 && (int)status <= 199;
	}

	/// <summary>
	/// 2xx
	/// </summary>
	public static bool IsSuccessful(HttpStatusCode status)
	{
		return (int)status >= 200 && (int)status <= 299;
	}
}
