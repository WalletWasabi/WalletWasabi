using Newtonsoft.Json;
using System.Threading.Tasks;

namespace System.Net.Http
{
	public static class HttpContentExtensions
	{
		public static async Task<T> ReadAsJsonAsync<T>(this HttpContent me)
		{
			if (me == null) return default;

			var jsonString = await me.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<T>(jsonString);
		}
	}
}
