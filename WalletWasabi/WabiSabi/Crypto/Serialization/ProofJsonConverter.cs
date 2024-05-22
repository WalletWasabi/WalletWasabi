using Newtonsoft.Json;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.WabiSabi.Crypto.Serialization;

public class ProofJsonConverter : JsonConverter<Proof>
{
	public override Proof? ReadJson(JsonReader reader, Type objectType, Proof? existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		reader.Expect(JsonToken.StartObject);
		var publicNonces = reader.ReadProperty<GroupElementVector>(serializer, "PublicNonces");
		var responses = reader.ReadProperty<ScalarVector>(serializer, "Responses");
		reader.Read();
		reader.Expect(JsonToken.EndObject);
		return ReflectionUtils.CreateInstance<Proof>(new object[] { publicNonces, responses });
	}

	/// <inheritdoc />
	public override void WriteJson(JsonWriter writer, Proof? proof, JsonSerializer serializer)
	{
		if (proof is null)
		{
			throw new ArgumentException($"No valid {nameof(Proof)}.", nameof(proof));
		}
		writer.WriteStartObject();
		writer.WriteProperty("PublicNonces", proof.PublicNonces, serializer);
		writer.WriteProperty("Responses", proof.Responses, serializer);
		writer.WriteEndObject();
	}
}
