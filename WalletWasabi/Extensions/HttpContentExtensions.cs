using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Serialization;

namespace WalletWasabi.Extensions;

public static class HttpContentExtensions
{
	public static async Task<T> ReadAsJsonAsync<T>(this HttpContent me, Decoder<T> decoder)
	{
		var jsonString = await me.ReadAsStringAsync().ConfigureAwait(false);
		return Decode.FromString(jsonString, decoder)
			?? throw new InvalidOperationException("'null' is forbidden.");
	}
}
