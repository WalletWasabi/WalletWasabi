using System.Collections.Generic;
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
		var publicNonces = reader.ReadProperty<GroupElementVector>(serializer, "publicNonces");
		var responses = reader.ReadProperty<ScalarVector>(serializer, "responses");
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
		writer.WriteProperty("publicNonces", proof.PublicNonces, serializer);
		writer.WriteProperty("responses", proof.Responses, serializer);
		writer.WriteEndObject();
	}
}
