using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters.Bitcoin;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

public class FeeRateJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"FeePaidSize":500000,"FeePerKByte":1000,"SatoshiPerByte":1000000,"Zero":0,"NoneFeeRate":null,"NotAnnotated":null}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "0";
			AssertBothDeserialize(token);

			token = "1";
			AssertBothDeserialize(token);

			token = "210000000"; // Maximum number of bitcoins ever to exist.
			AssertBothDeserialize(token);

			token = "9223372036854775807"; // Biggest long number
			AssertBothDeserialize(token);
		}

		// Format exception errors.
		{
			// The old converter throws InvalidCastException, the new converter throws JsonException.
			string invalidToken = "1e6";
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(invalidToken);

			// The old converter throws JsonReaderException, the new converter throws JsonException.
			invalidToken = "1,0";
			AssertDeserializeDifferentExceptions<JsonReaderException, System.Text.Json.JsonException>(invalidToken);

			// The old converter throws JsonReaderException, the new converter throws JsonException.
			invalidToken = "1,000.00";
			AssertDeserializeDifferentExceptions<JsonReaderException, System.Text.Json.JsonException>(invalidToken);
		}

		// Unique case.
		{
			string invalidToken = "1."; // No digit after decimal point.
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(invalidToken);
		}

		// Tests that neither JSON converter can deserialize a JSON number-string instead of a JSON integer.
		{
			string invalidToken = ConvertToJsonString("100");
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(invalidToken);
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "Little Book of Calm", "Fee": {{jsonToken}} }""";

			TestRecord? record1 = JsonConvertOld.DeserializeObject<TestRecord>(json);
			TestRecord? record2 = JsonConvertNew.Deserialize<TestRecord>(json);

			// Value equality.
			Assert.Equal(record1, record2);
		}

		static void AssertDeserializeDifferentExceptions<TExceptionOld, TExceptionNew>(string jsonToken)
			where TExceptionOld : Exception
			where TExceptionNew : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Fee": {{jsonToken}} }""";

			Assert.Throws<TExceptionOld>(() => JsonConvertOld.DeserializeObject<TestRecord>(json));
			Assert.Throws<TExceptionNew>(() => JsonConvertNew.Deserialize<TestRecord>(json));
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
	/// Record for testing deserialization of <see cref="FeeRate"/>.
	/// </summary>
	private record TestRecord
	{
		public required string Name { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Fee))]
		[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Fee))]
		public FeeRate? Fee { get; init; }
	}

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	private record TestData
	{
		//FeeRate
		[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(FeePaidSize))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(FeePaidSize))]
		[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
		public FeeRate FeePaidSize { get; set; } = new(Money.Satoshis(1000), 2);

		[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(FeePerKByte))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(FeePerKByte))]
		[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
		public FeeRate FeePerKByte { get; set; } = new(Money.Satoshis(1000));

		[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SatoshiPerByte))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SatoshiPerByte))]
		[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
		public FeeRate SatoshiPerByte { get; set; } = new((decimal)1000);

		[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Zero))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Zero))]
		[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
		public FeeRate Zero { get; set; } = new(Money.Zero);

		[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(NoneFeeRate))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(NoneFeeRate))]
		[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
		public FeeRate? NoneFeeRate { get; set; } = null;

		/// <summary>Not annotated properties are also included by both Newtonsoft and STJ by default.</summary>
		public Money? NotAnnotated { get; set; } = null;
	}
}
