
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Http.Extensions;

public static class HttpContentExtensions
{
	public static async Task<T> ReadAsJsonAsync<T>(this HttpContent me, CancellationToken cancellationToken)
	{
		if (me is null)
		{
			throw new ArgumentNullException(nameof(me));
		}

		var jsonString = await me.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return JsonConvert.DeserializeObject<T>(jsonString)
		       ?? throw new JsonSerializationException($"Deserialization failed. Received json: {jsonString}");
	}
}
