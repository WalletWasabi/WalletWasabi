using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.JsonConverters;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tor.Http.Extensions;

public static class HttpContentExtensions
{
	private static readonly JsonSerializerSettings Settings = new();

	public static async Task<T> ReadAsJsonAsync<T>(this HttpContent me)
	{
		if (me is null)
		{
			throw new ArgumentNullException(nameof(me));
		}

		var jsonString = await me.ReadAsStringAsync().ConfigureAwait(false);
		return JsonConvert.DeserializeObject<T>(jsonString, Settings)
			?? throw new InvalidOperationException($"Deserialization failed. Received json: {jsonString}");
	}
}
