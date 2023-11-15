using NBitcoin;
using WalletWasabi.JsonConverters;
using WalletWasabi.Tests.Helpers;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

/// <summary>
/// Tests for <see cref="ExtPubKeyJsonConverter"/> and <see cref="ExtPubKeyJsonConverterNg"/> classes.
/// </summary>
public class ExtPubKeyJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		var km = ServiceFactory.CreateKeyManager(isTaprootAllowed: true);

		TestData testObject = new() { SegwitExtPubKey = km.SegwitExtPubKey, TaprootExtPubKey = km.TaprootExtPubKey };
		var segwit = km.SegwitExtPubKey.ToString(Network.Main);
		var taproot = km.TaprootExtPubKey!.ToString(Network.Main);

		string json = AssertSerializedEqually(testObject);
		Assert.Equal($$"""{"SegwitExtPubKey":"{{segwit}}","TaprootExtPubKey":"{{taproot}}"}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "xpub6CzZinqjT2VBCDQNqB1Y7saFqaMHYg54C6BCbLSnDWirx3EBAVNp8HANg1xJFYLR1fNGcfcWqirZ88GXEYdhh9rd1AyWceTDoZJ7GNxzx2K"; // SegwitExtPubKey
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "xpub6Ch4bJcGsTXbw5P7gCutJUC8FPxaGV5ps59Hquj1Boypx9DZcR7JFp4uCYiMGgcxkJJuKkT6kNJjCZVSEBBuyQjZ7FtaJBH3WnVtjucFsin"; // TaprootExtPubKey
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "null"; // null value
			AssertBothDeserialize(token);
		}

		// Failing cases.
		{
			string invalidToken = "xpub6CzZinqjT2VBCDQNqB1Y7saFqaMHYg55C6BCbLSnDWirx3EBAVNp8HANg1xJFYLR1fNGcfcWqirZ88GXEYdhh9rd1AyWceTDoZJ7GNxzx2K"; // Changed one letter of the first example
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));

			invalidToken = "xpub1CzZinqjT2VBCDQNqB1Y7saFqaMHYg55C6BCbLSnDWirx3EBAVNp8HANg1xJFYLR1fNGcfcWqirZ88GXEYdhh9rd1AyWceTDoZJ7GNxzx2K"; // Changed xpub6 to xpub1
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));

			invalidToken = "xpub6CzZinqjT2VBCDQNqB1Y7saFqaMHYg55C6BCbLSnDWirx"; // Cut off half of the key
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));

			invalidToken = "null"; // "null" as string
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "Extended Public Key", "ExtPubKey": {{jsonToken}} }""";

			TestRecord? record1 = JsonConvertOld.DeserializeObject<TestRecord>(json);
			TestRecord? record2 = JsonConvertNew.Deserialize<TestRecord>(json);

			// Value equality.
			Assert.Equal(record1, record2);
		}

		static void AssertDeserializeFailure<TException>(string jsonToken)
			where TException : Exception
		{
			string json = $$"""{"Name": "Extended Public Key", "ExtPubKey": {{jsonToken}} }""";

			Assert.Throws<TException>(() => JsonConvertOld.DeserializeObject<TestRecord>(json));
			Assert.Throws<TException>(() => JsonConvertNew.Deserialize<TestRecord>(json));
		}

		static string ConvertToJsonString(string s)
			=> $"\"{s}\"";
	}

	/// <summary>
	/// Asserts that object <paramref name="o"/> is serialized to the same JSON by both Newtonsoft.Json and STJ library.
	/// </summary>
	/// <returns>JSON representation of <paramref name="o"/>.</returns>
	private static string AssertSerializedEqually<T>(T o)
	{
		string newtonsoftJson = JsonConvertOld.SerializeObject(o);
		string stjJson = JsonConvertNew.Serialize(o);

		Assert.NotNull(newtonsoftJson);
		Assert.NotNull(stjJson);
		Assert.Equal(newtonsoftJson, stjJson);

		return stjJson;
	}

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	private record TestData
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SegwitExtPubKey))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SegwitExtPubKey))]
		public ExtPubKey? SegwitExtPubKey { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(TaprootExtPubKey))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(TaprootExtPubKey))]
		public ExtPubKey? TaprootExtPubKey { get; init; }
	}

	/// <summary>
	/// Record for testing deserialization of <see cref="ExtPubKey"/>.
	/// </summary>
	private record TestRecord
	{
		public required string Name { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(ExtPubKey))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(ExtPubKey))]
		public ExtPubKey? ExtPubKey { get; init; }
	}
}
