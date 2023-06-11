using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Http.Extensions;

public static class HttpContentExtensions
{
	private static readonly JsonSerializerSettings Settings = new();

	/// <exception cref="JsonException">If JSON deserialization fails for any reason.</exception>
	/// <exception cref="InvalidOperationException">If the JSON string is <c>"null"</c> (valid value but forbiden in this implementation).</exception>
	public static async Task<T> ReadAsJsonAsync<T>(this HttpContent me)
	{
		var jsonString = await me.ReadAsStringAsync().ConfigureAwait(false);
		return JsonConvert.DeserializeObject<T>(jsonString, Settings)
			?? throw new InvalidOperationException("'null' is forbidden.");
	}
}
