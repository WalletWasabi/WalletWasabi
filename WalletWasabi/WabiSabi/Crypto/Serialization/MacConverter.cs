using System.Collections.Generic;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class MacJsonConverter : JsonConverter<MAC>
{
	public override MAC? ReadJson(JsonReader reader, Type objectType, MAC? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		reader.Expect(JsonToken.StartObject);
		var t = reader.ReadProperty<Scalar>(serializer, "T");
		var v = reader.ReadProperty<GroupElement>(serializer, "V");
		reader.Read();
		reader.Expect(JsonToken.EndObject);
		return ReflectionUtils.CreateInstance<MAC>(new object[] { t, v });
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, MAC? mac, JsonSerializer serializer)
	{
		if (mac is null)
		{
			throw new ArgumentException($"No valid {nameof(MAC)}.", nameof(mac));
		}
		writer.WriteStartObject();
		writer.WriteProperty("T", mac.T, serializer);
		writer.WriteProperty("V", mac.V, serializer);
		writer.WriteEndObject();
	}
}
