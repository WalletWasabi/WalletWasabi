using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Extensions;
using WalletWasabi.Models;

namespace WalletWasabi.JsonConverters;

/// <summary>
/// Converter used to convert <see cref="ChangeScriptPubKeyType"/> to and from JSON.
/// </summary>
/// <seealso cref="JsonConverter" />
public class PreferredScriptPubKeyTypeJsonConverter : JsonConverter<PreferredScriptPubKeyType>
{
	/// <inheritdoc />
	public override PreferredScriptPubKeyType ReadJson(JsonReader reader, Type objectType, PreferredScriptPubKeyType? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var preferredScriptPubKeyString = ((string?)reader.Value)?.Trim();

		if (preferredScriptPubKeyString is null)
		{
			throw new ArgumentNullException(nameof(preferredScriptPubKeyString));
		}

		if(preferredScriptPubKeyString == PreferredScriptPubKeyType.Unspecified.Instance.Name)
		{
			return PreferredScriptPubKeyType.Unspecified.Instance;
		}

		if (Enum.TryParse(preferredScriptPubKeyString, true, out ScriptPubKeyType result))
		{
			return new PreferredScriptPubKeyType.Specified(result);
		}

		throw new JsonSerializationException($"Invalid ChangeScriptPubKeyType: {preferredScriptPubKeyString}");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, PreferredScriptPubKeyType? value, JsonSerializer serializer)
	{
		var scriptPubKeyType = value switch
		{
			PreferredScriptPubKeyType.Unspecified => "Random",
			PreferredScriptPubKeyType.Specified scriptType => scriptType.ScriptType.FriendlyName(),
			_ => throw new ArgumentOutOfRangeException()
		};

		writer.WriteValue(scriptPubKeyType);
	}
}
