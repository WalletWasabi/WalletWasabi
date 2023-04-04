using Newtonsoft.Json;
using WabiSabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class GroupElementJsonConverter : JsonConverter<GroupElement>
{
	/// <inheritdoc />
	public override GroupElement? ReadJson(JsonReader reader, Type objectType, GroupElement? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return GroupElement.FromBytes(ByteHelpers.FromHex(serialized));
		}
		throw new ArgumentException($"No valid serialized {nameof(GroupElement)} passed.");
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, GroupElement? value, JsonSerializer serializer)
	{
		if (value is { } ge)
		{
			writer.WriteValue(ByteHelpers.ToHex(ge.ToBytes()));
			return;
		}
		throw new ArgumentException($"No valid {nameof(GroupElement)}.", nameof(value));
	}
}
