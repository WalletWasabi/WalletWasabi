using Newtonsoft.Json;
using WalletWasabi.Crypto.Groups;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class GroupElementJsonConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(GroupElement);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return GroupElement.FromBytes(ByteHelpers.FromHex(serialized));
		}
		throw new ArgumentException($"No valid serialized {nameof(GroupElement)} passed.");
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		if (value is GroupElement ge)
		{
			writer.WriteValue(ByteHelpers.ToHex(ge.ToBytes()));
			return;
		}
		throw new ArgumentException($"No valid {nameof(GroupElement)}.", nameof(value));
	}
}
