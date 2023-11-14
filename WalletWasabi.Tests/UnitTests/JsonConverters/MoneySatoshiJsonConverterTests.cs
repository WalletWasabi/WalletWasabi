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
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"OneCoin":100000000,"OneSatoshi":1,"Zeros":100,"Max":2099999997690000,"None":null,"NotAnnotated":null}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *deserialize* objects equally.
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
			string token = "1,000.00"; // Thousand separator.
			AssertDeserializeJsonException(token);

			token = "1,0"; // Decimal comma.
			AssertDeserializeJsonException(token);
		}

		// Casting errors Vs Format Exception.
		{
			// Newtonsoft gives back InvalidCastException, Microsoft gives back FormatException.
			string token = "0.00000000000000000000000000000000000000000000001";
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(token);

			// Newtonsoft gives back InvalidCastException, Microsoft gives back FormatException.
			token = "209999999.97690001"; // Maximum number of bitcoins ever to exist + 1 satoshi.
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(token);

			// Newtonsoft gives back InvalidCastException, Microsoft gives back FormatException.
			token = "1e6"; // Exponential notation.
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(token);
		}

		// Unique case.
		{
			// Newtonsoft gives back InvalidCastException, Microsoft gives back JsonException (Read function not even called).
			string token = "1."; // No digit after decimal point.
			AssertDeserializeDifferentExceptions<InvalidCastException, System.Text.Json.JsonException>(token);

			// TODO: remove https://stackoverflow.com/questions/27361565/why-is-json-invalid-if-an-integer-begins-with-a-leading-zero
			// https://www.rfc-editor.org/rfc/rfc4627.txt - section 2.4. Numbers mentions "Leading zeros are not allowed."
			// Newtonsoft reads 0, Microsoft gives back JsonException.
			// token = "00000000000000000000000";
			// AssertBothDeserialize(token);
		}

		// Tests that neither JSON converters can deserialize to NULL if a JSON number-string is found instead of a JSON integer.
		{
			string token = "2999";
			AssertDeserializeFailure<InvalidCastException>(S(token));
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			TestProduct? product1 = JsonConvertOld.DeserializeObject<TestProduct>(json);
			TestProduct? product2 = JsonConvertNew.Deserialize<TestProduct>(json);

			// Value equality.
			Assert.Equal(product1, product2);
		}

		static void AssertDeserializeFailure<TException>(string jsonToken)
			where TException : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			Assert.Throws<TException>(() => JsonConvertOld.DeserializeObject<TestProduct>(json));
			Assert.Throws<TException>(() => JsonConvertNew.Deserialize<TestProduct>(json));
		}

		static void AssertDeserializeDifferentExceptions<TExceptionOld, TExceptionNew>(string jsonToken)
			where TExceptionOld : Exception
			where TExceptionNew : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			Assert.Throws<TExceptionOld>(() => JsonConvertOld.DeserializeObject<TestProduct>(json));
			Assert.Throws<TExceptionNew>(() => JsonConvertNew.Deserialize<TestProduct>(json));
		}

		static void AssertDeserializeJsonException(string jsonToken)
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": {{jsonToken}} }""";

			Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JsonConvertOld.DeserializeObject<TestProduct>(json));
			Assert.Throws<System.Text.Json.JsonException>(() => JsonConvertNew.Deserialize<TestProduct>(json));
		}

		static string S(string s)
			=> $"\"{s}\"";
	}

	/// <summary>
	/// Asserts that object <paramref name="o"/> is serialized to the same JSON by both Newtonsoft library and STJ library.
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
	private record TestProduct
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
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Zeros))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Zeros))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money Zeros { get; set; } = Money.Coins(0.000001m);

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
