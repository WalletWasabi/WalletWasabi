using NBitcoin;
using WalletWasabi.JsonConverters.Bitcoin;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters
{
	public class FeeRateJsonConverterTests
	{
		/// <summary>
		/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *serialize* objects equally.
		/// </summary>
		[Fact]
		public void SerializationParity()
		{
			TestData testObject = new();

			string json = AssertSerializedEqually(testObject);
			Assert.Equal(
				"""{"FeePaidSize":500000,"FeePerKByte":1000,"SatoshiPerByte":1000000,"Zero":0,"NoneFeeRate":null,"NotAnnotated":null}""",
				json);
		}

		/// <summary>
		/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *deserialize* objects equally.
		/// </summary>
		[Fact]
		public void DeserializationParity()
		{
			// Success cases.
			{
				string token = "209999999"; // Maximum number of bitcoins ever to exist + 1 satoshi.
				AssertBothDeserialize(S(token));

				token = "1";
				AssertBothDeserialize(S(token));

				token = "9223372036854775807"; // Biggest long number
				AssertBothDeserialize(S(token));

				token = "0";
				AssertBothDeserialize(S(token));

				token = "00000000000000";
				AssertBothDeserialize(S(token));
			}

			// Format exception errors.
			{
				string token = "1e6";
				//OldConverter - InvalidCastException, newConverter - JsonException
				AssertDeserializeFailure<FormatException>(S(token));

				//OldConverter - JsonReaderException, newConverter - JsonException
				token = "1,0";
				AssertDeserializeFailure<FormatException>(S(token));

				//OldConverter - JsonReaderException  newConverter - JsonException
				token = "1,000.00";
				AssertDeserializeFailure<FormatException>(S(token));
			}

			// Unique case.
			{
				// Different exceptions
				 string token = "1."; // No digit after decimal point.
				 AssertBothDeserialize(S(token));
			}


			static void AssertBothDeserialize(string jsonToken)
			{
				string json = $$"""{"Name": "Little Book of Calm", "Fee": {{jsonToken}} }""";

				TestProduct? product1 = JsonConvertOld.DeserializeObject<TestProduct>(json);
				TestProduct? product2 = JsonConvertNew.Deserialize<TestProduct>(json);

				// Value equality.
				Assert.Equal(product1, product2);
			}

			static void AssertDeserializeFailure<TException>(string jsonToken)
				where TException : Exception
			{
				string json = $$"""{"Name": "Little Book of Calm", "Fee": {{jsonToken}} }""";

				Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JsonConvertOld.DeserializeObject<TestProduct>(json));
				Assert.Throws<System.Text.Json.JsonException>(() => JsonConvertNew.Deserialize<TestProduct>(json));
			}

			static string S(string s)
				=> $"{s}";
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
		/// Record for testing deserialization of <see cref="FeeRate"/>.
		/// </summary>
		private record TestProduct
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
			public FeeRate FeePaidSize { get; set; } = new (Money.Satoshis(1000), 2);

			[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
			[System.Text.Json.Serialization.JsonPropertyName(nameof(FeePerKByte))]
			[Newtonsoft.Json.JsonProperty(PropertyName = nameof(FeePerKByte))]
			[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
			public FeeRate FeePerKByte { get; set; } = new (Money.Satoshis(1000));

			[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
			[System.Text.Json.Serialization.JsonPropertyName(nameof(SatoshiPerByte))]
			[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SatoshiPerByte))]
			[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
			public FeeRate SatoshiPerByte { get; set; } = new ((decimal)1000);

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
}
