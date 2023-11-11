using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

/// <summary>
/// Tests for <see cref="MoneyBtcJsonConverter"/> and <see cref="MoneyBtcJsonConverterNg"/> classes.
/// </summary>
public class MoneyBtcJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"Half":"0.50","One":"1.00","Zeros":"0.000001","Max":"20999999.9769","None":null,"NotAnnotated":null}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "209999999.97690001"; // Maximum number of bitcoins ever to exist + 1 satoshi.
			AssertBothDeserialize(S(token));

			token = "210000000"; // 21 million bitcoin.
			AssertBothDeserialize(S(token));

			token = "0.00000000000000000000000000000000000000000000001";
			AssertBothDeserialize(S(token));

			token = "00000000000000000000000";
			AssertBothDeserialize(S(token));
		}

		// Format exception errors.
		{
			string token = "1e6"; // Exponential notation.
			AssertDeserializeFailure<FormatException>(S(token));

			token = "1,0"; // Decimal comma.
			AssertDeserializeFailure<FormatException>(S(token));

			token = "1,000.00"; // Thousand separator.
			AssertDeserializeFailure<FormatException>(S(token));
		}

		// Unique case.
		{
			string token = "1."; // No digit after decimal point.
			AssertBothDeserialize(S(token));
		}

		// Tests that both JSON converters deserialize to NULL if a JSON integer is found instead of a JSON number-string.
		{
			string json = $$"""{"Name": "Little Book of Calm", "Price": 29.99 }""";

			// Old.
			{
				TestProduct? product = JsonConvertOld.DeserializeObject<TestProduct>(json);
				Assert.NotNull(product);
				Assert.Null(product.Price);
			}

			// New.
			{
				TestProduct? product = JsonConvertNew.Deserialize<TestProduct>(json);
				Assert.NotNull(product);
				Assert.Null(product.Price);
			}
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
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Price))]
		public Money? Price { get; init; }
	}

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	private record TestData
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Half))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Half))]
		public Money Half { get; set; } = Money.Coins(0.500m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(One))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(One))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money One { get; set; } = Money.Coins(1m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Zeros))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Zeros))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Zeros { get; set; } = Money.Coins(0.000001m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Max))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Max))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Max { get; set; } = Money.Coins(Constants.MaximumNumberOfBitcoins);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(None))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(None))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money? None { get; set; } = null;

		/// <summary>Not annotated properties are also included by both Newtonsoft and STJ by default.</summary>
		public Money? NotAnnotated { get; set; } = null;
	}
}
