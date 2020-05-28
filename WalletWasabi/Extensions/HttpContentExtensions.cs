using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Threading.Tasks;
using WalletWasabi.JsonConverters;
using WalletWasabi.WebClients.Wasabi;

namespace System.Net.Http
{
	public static class HttpContentExtensions
	{
		public static async Task<T> ReadAsJsonAsync<T>(this HttpContent me)
		{
			if (me is null)
			{
				return default;
			}

			var settings = new JsonSerializerSettings
			{
				Converters = new[] { new RoundStateResponseJsonConverter(WasabiClient.ApiVersion) }
			};
			var jsonString = await me.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<T>(jsonString, settings);
		}
	}
}
