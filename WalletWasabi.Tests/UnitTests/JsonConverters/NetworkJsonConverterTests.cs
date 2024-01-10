using NBitcoin;
using System.Text.Json;
using WalletWasabi.JsonConverters;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

/// <summary>
/// Tests for <see cref="NetworkJsonConverter"/> and <see cref="NetworkJsonConverterNg"/> classes.
/// </summary>
public class NetworkJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"Main":"Main","Test":"TestNet","RegTest":"RegTest","Default":null,"NotAnnotated":null}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "Main";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "Mainnet";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "TestNet";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "RegTest";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "regression";
			AssertBothDeserialize(ConvertToJsonString(token)); // ~ RegTest.

			token = "     MainNet     "; // JSON convertors handle whitespace by trimming it.
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "Test";
			AssertBothDeserialize(ConvertToJsonString(token)); // ~ TestNet.

			token = "Reg";
			AssertBothDeserialize(ConvertToJsonString(token)); // ~ RegTest.

			token = "Man";
			AssertBothDeserialize(ConvertToJsonString(token)); // null.

			token = " ";
			AssertBothDeserialize(ConvertToJsonString(token)); // null.

			token = "null";
			AssertBothDeserialize(ConvertToJsonString(token)); // null.
		}

		// Failing cases.
		{
			string invalidToken = "100";
			AssertDeserializeDifferentExceptions<InvalidCastException, JsonException>(invalidToken);
		}

		// Unique cases.
		{
			string invalidToken = "null";
			AssertDeserializeFailure<ArgumentNullException>(invalidToken);
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "Network", "Network": {{jsonToken}} }""";

			TestRecord? record1 = JsonConvertOld.DeserializeObject<TestRecord>(json);
			TestRecord? record2 = JsonConvertNew.Deserialize<TestRecord>(json);

			// Value equality.
			Assert.Equal(record1, record2);
		}

		static void AssertDeserializeFailure<TException>(string jsonToken)
			where TException : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Network": {{jsonToken}} }""";

			Assert.Throws<TException>(() => JsonConvertOld.DeserializeObject<TestRecord>(json));
			Assert.Throws<TException>(() => JsonConvertNew.Deserialize<TestRecord>(json));
		}

		static void AssertDeserializeDifferentExceptions<TExceptionOld, TExceptionNew>(string jsonToken)
			where TExceptionOld : Exception
			where TExceptionNew : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Network": {{jsonToken}} }""";

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
	/// Record for testing deserialization of <see cref="NBitcoin.Network"/>.
	/// </summary>
	private record TestRecord
	{
		public required string Name { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Network))]
		[Newtonsoft.Json.JsonConverter(typeof(NetworkJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(NetworkJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Network))]
		public Network? Network { get; init; }
	}

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	private record TestData
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Main))]
		[Newtonsoft.Json.JsonConverter(typeof(NetworkJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(NetworkJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Main))]
		public Network Main { get; set; } = Network.Main;

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Test))]
		[Newtonsoft.Json.JsonConverter(typeof(NetworkJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(NetworkJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Test))]
		public Network Test { get; set; } = Network.TestNet;

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(RegTest))]
		[Newtonsoft.Json.JsonConverter(typeof(NetworkJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(NetworkJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(RegTest))]
		public Network RegTest { get; set; } = Network.RegTest;

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Default))]
		[Newtonsoft.Json.JsonConverter(typeof(NetworkJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(NetworkJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Default))]
		public Network? Default { get; set; }

		public Network? NotAnnotated { get; set; } = null;
	}
}
