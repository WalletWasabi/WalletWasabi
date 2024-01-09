using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

/// <summary>
/// Converter used to convert <see cref="Network"/> to and from JSON.
/// </summary>
/// <seealso cref="JsonConverter" />
public class NetworkJsonConverter : JsonConverter<Network>
{
	/// <inheritdoc />
	public override Network? ReadJson(JsonReader reader, Type objectType, Network? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		// check additional strings that are not checked by GetNetwork
		var networkString = ((string?)reader.Value)?.Trim();

		if (networkString is null)
		{
			throw new ArgumentNullException(nameof(networkString));
		}

		if ("regression".Equals(networkString, StringComparison.OrdinalIgnoreCase))
		{
			return Network.RegTest;
		}

		return Network.GetNetwork(networkString);
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Network? value, JsonSerializer serializer)
	{
		string network = value?.ToString()
			?? throw new ArgumentNullException(nameof(value));

		writer.WriteValue(network);
	}
}
