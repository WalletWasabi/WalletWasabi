using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.JsonConverters;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Tor.Http.Extensions;

public static class HttpContentExtensions
{
	public static async Task<T> ReadAsJsonAsync<T>(this HttpContent me)
	{
		if (me is null)
		{
			throw new ArgumentNullException(nameof(me));
		}

		var settings = new JsonSerializerSettings
		{
			Converters = new[] { new RoundStateResponseJsonConverter(WasabiClient.ApiVersion) }
		};
		var jsonString = await me.ReadAsStringAsync().ConfigureAwait(false);
		return JsonConvert.DeserializeObject<T>(jsonString, settings)
			?? throw new InvalidOperationException($"Deserialization failed. Received json: {jsonString}");
	}
}
