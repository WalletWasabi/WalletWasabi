using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class HDFingerprintJsonConverter : JsonConverter<HDFingerprint?>
{
	/// <inheritdoc />
	public override HDFingerprint? ReadJson(JsonReader reader, Type objectType, HDFingerprint? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var s = (string?)reader.Value;
		if (string.IsNullOrWhiteSpace(s))
		{
			return null;
		}

		return new HDFingerprint(ByteHelpers.FromHex(s));
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, HDFingerprint? value, JsonSerializer serializer)
	{
		var stringValue = value?.ToString() ?? throw new ArgumentNullException(nameof(value));
		writer.WriteValue(stringValue);
	}
}
