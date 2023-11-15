using NBitcoin;
using WalletWasabi.JsonConverters.Timing;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

/// <summary>
/// Tests for <see cref="TimeSpanJsonConverter"/> and <see cref="TimeSpanJsonConverterNg"/> classes.
/// </summary>
public class TimeSpanJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"DefaultTime":"0d 0h 0m 0s","OneHour":"0d 1h 0m 0s","OneDay":"1d 0h 0m 0s","MaxValue":"10675199d 2h 48m 5s","MinValue":"-10675199d -2h -48m -5s","None":null,"NotAnnotated":"00:00:00"}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "0d 0h 0m 0s";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "400d 0h 0m 0s";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "0d 0h 0m 0s 10u";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "10d10h10m10s";
			AssertBothDeserialize(ConvertToJsonString(token));
		}

		// Failing cases.
		{
			// Valid input is: <days>d <hours>h <minutes>m <seconds>s. All other variants are invalid.
			string invalidToken = "1440";
			AssertDeserializeFailure<IndexOutOfRangeException>(ConvertToJsonString(invalidToken));

			invalidToken = "367d";
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));

			invalidToken = "0h 0m 0s";
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));

			invalidToken = "0s 0m 0h 0d";
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));

			invalidToken = "00:00:00";
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "DateTime", "Date": {{jsonToken}} }""";

			TestRecord? record1 = JsonConvertOld.DeserializeObject<TestRecord>(json);
			TestRecord? record2 = JsonConvertNew.Deserialize<TestRecord>(json);

			// Value equality.
			Assert.Equal(record1, record2);
		}

		static void AssertDeserializeFailure<TException>(string jsonToken)
			where TException : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Date": {{jsonToken}} }""";

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
	/// Record for testing deserialization of <see cref="TimeSpan"/>.
	/// </summary>
	private record TestRecord
	{
		public required string Name { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Date))]
		[Newtonsoft.Json.JsonConverter(typeof(TimeSpanJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(TimeSpanJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Date))]
		public TimeSpan? Date { get; init; }
	}

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	private record TestData
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(DefaultTime))]
		[Newtonsoft.Json.JsonConverter(typeof(TimeSpanJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(TimeSpanJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(DefaultTime))]
		public TimeSpan DefaultTime { get; set; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(OneHour))]
		[Newtonsoft.Json.JsonConverter(typeof(TimeSpanJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(TimeSpanJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(OneHour))]
		public TimeSpan OneHour { get; set; } = TimeSpan.FromHours(1);

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(OneDay))]
		[Newtonsoft.Json.JsonConverter(typeof(TimeSpanJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(TimeSpanJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(OneDay))]
		public TimeSpan OneDay { get; set; } = TimeSpan.FromDays(1);

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(MaxValue))]
		[Newtonsoft.Json.JsonConverter(typeof(TimeSpanJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(TimeSpanJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(MaxValue))]
		public TimeSpan MaxValue { get; set; } = TimeSpan.MaxValue;

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(MinValue))]
		[Newtonsoft.Json.JsonConverter(typeof(TimeSpanJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(TimeSpanJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(MinValue))]
		public TimeSpan MinValue { get; set; } = TimeSpan.MinValue;

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(None))]
		[Newtonsoft.Json.JsonConverter(typeof(TimeSpanJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(TimeSpanJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(None))]
		public TimeSpan? None { get; set; } = null;

		public TimeSpan NotAnnotated { get; set; } = TimeSpan.Zero;
	}
}
