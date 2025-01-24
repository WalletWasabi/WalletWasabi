using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Extensions;

namespace WalletWasabi.JsonConverters;

/// <summary>
/// Converter used to convert <see cref="ScriptPubKeyType"/> to and from JSON.
/// </summary>
/// <seealso cref="JsonConverter" />
public class ScriptPubKeyTypeJsonConverter : JsonConverter<ScriptPubKeyType>
{
	/// <inheritdoc />
	public override ScriptPubKeyType ReadJson(JsonReader reader, Type objectType, ScriptPubKeyType existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var scriptPubKeyString = ((string?)reader.Value)?.Trim();

		if (scriptPubKeyString is null)
		{
			throw new ArgumentNullException(nameof(scriptPubKeyString));
		}

		if (Enum.TryParse(scriptPubKeyString, true, out ScriptPubKeyType result))
		{
			return result;
		}

		throw new JsonSerializationException($"Invalid ScriptPubKeyType: {scriptPubKeyString}");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, ScriptPubKeyType value, JsonSerializer serializer)
	{
		string scriptPubKeyType = value.FriendlyName()
		                 ?? throw new ArgumentNullException(nameof(value));

		writer.WriteValue(scriptPubKeyType);
	}
}
