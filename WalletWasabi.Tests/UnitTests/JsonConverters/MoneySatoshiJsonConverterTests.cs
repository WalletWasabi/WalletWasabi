using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

public class MoneySatoshiJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"OneCoin":100000000,"OneSatoshi":1,"SmallAmount":100,"Zeros":0,"Max":2099999997690000,"None":null,"NotAnnotated":null}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "2100000000000000"; // 21 million.
			AssertBothDeserialize(token);
		}

		// Json exceptions.
		// Newtonsoft gives JsonReaderException, Microsoft gives JsonException. Similar but not the same.
		{
			string invalidToken = "1,000.00"; // Thousand separator.
			AssertDeserializeJsonException(invalidToken);

			invalidToken = "1,0"; // Decimal comma.
			AssertDeserializeJsonException(invalidToken);
		}

		// Casting errors Vs Format Exception.
		{
			// Newtonsoft gives back InvalidCastException, Microsoft gives back FormatException.
			string invalidToken = "0.00000000000000000000000000000000000000000000001";
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(invalidToken);

			// Newtonsoft gives back InvalidCastException, Microsoft gives back FormatException.
			invalidToken = "209999999.97690001"; // Maximum number of bitcoins ever to exist + 1 satoshi.
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(invalidToken);

			// Newtonsoft gives back InvalidCastException, Microsoft gives back FormatException.
			invalidToken = "1e6"; // Exponential notation.
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(invalidToken);
		}

		// Unique case.
		{
			// Newtonsoft gives back InvalidCastException, Microsoft gives back JsonException (Read function not even called).
			string invalidToken = "1."; // No digit after decimal point.
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(invalidToken);

			// TODO: remove https://stackoverflow.com/questions/27361565/why-is-json-invalid-if-an-integer-begins-with-a-leading-zero
			// https://www.rfc-editor.org/rfc/rfc4627.txt - section 2.4. Numbers mentions "Leading zeros are not allowed."
			// Newtonsoft reads 0, Microsoft gives back JsonException.
			// token = "00000000000000000000000";
			// AssertBothDeserialize(token);
		}

		// Tests that neither JSON converter can deserialize a JSON number-string instead of a JSON integer.
		{
			string invalidToken = ConvertToJsonString("2999");
			AssertDeserializeFailure<InvalidCastException>(invalidToken);
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			TestRecord? record1 = JsonConvertOld.DeserializeObject<TestRecord>(json);
			TestRecord? record2 = JsonConvertNew.Deserialize<TestRecord>(json);

			// Value equality.
			Assert.Equal(record1, record2);
		}

		static void AssertDeserializeFailure<TException>(string jsonToken)
			where TException : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			Assert.Throws<TException>(() => JsonConvertOld.DeserializeObject<TestRecord>(json));
			Assert.Throws<TException>(() => JsonConvertNew.Deserialize<TestRecord>(json));
		}

		static void AssertDeserializeDifferentExceptions<TExceptionOld, TExceptionNew>(string jsonToken)
			where TExceptionOld : Exception
			where TExceptionNew : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			Assert.Throws<TExceptionOld>(() => JsonConvertOld.DeserializeObject<TestRecord>(json));
			Assert.Throws<TExceptionNew>(() => JsonConvertNew.Deserialize<TestRecord>(json));
		}

		static void AssertDeserializeJsonException(string jsonToken)
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JsonConvertOld.DeserializeObject<TestRecord>(json));
			Assert.Throws<System.Text.Json.JsonException>(() => JsonConvertNew.Deserialize<TestRecord>(json));
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
	/// Record for testing deserialization of <see cref="Money"/>.
	/// </summary>
	private record TestRecord
	{
		public required string Name { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Price))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Price))]
		public Money? Price { get; init; }
	}

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	private record TestData
	{
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(OneCoin))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(OneCoin))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money OneCoin { get; set; } = Money.Coins(1m);

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(OneSatoshi))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(OneSatoshi))]
		public Money OneSatoshi { get; set; } = Money.Satoshis(1m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SmallAmount))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SmallAmount))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money SmallAmount { get; set; } = Money.Coins(0.000001m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Zeros))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Zeros))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money Zeros { get; set; } = Money.Zero;

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Max))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Max))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money Max { get; set; } = Money.Coins(Constants.MaximumNumberOfBitcoins);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(None))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(None))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money? None { get; set; } = null;

		/// <summary>Not annotated properties are also included by both Newtonsoft and STJ by default.</summary>
		public Money? NotAnnotated { get; set; } = null;
	}
}
