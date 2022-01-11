using NBitcoin;
using Newtonsoft.Json;

namespace WalletWasabi.JsonConverters;

public class HDFingerprintJsonConverter : JsonConverter<HDFingerprint>
{
	/// <inheritdoc />
	public override HDFingerprint ReadJson(JsonReader reader, Type objectType, HDFingerprint existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		var s = (string?)reader.Value;
		if (string.IsNullOrWhiteSpace(s))
		{
			return default;
		}

		var fp = new HDFingerprint(ByteHelpers.FromHex(s));
		return fp;
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, HDFingerprint value, JsonSerializer serializer)
	{
		writer.WriteValue(value.ToString());
	}
}
