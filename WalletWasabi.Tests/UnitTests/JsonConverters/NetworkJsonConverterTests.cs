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
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"Main":"Main","Test":"TestNet","RegTest":"RegTest","Default":null,"NotAnnotated":null}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>NewtonSoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "Main";
			AssertBothDeserialize(S(token));

			token = "TestNet";
			AssertBothDeserialize(S(token));

			token = "RegTest";
			AssertBothDeserialize(S(token));

			token = "regression";
			AssertBothDeserialize(S(token)); // Regtest

			token = "Test";
			AssertBothDeserialize(S(token)); // Testnet

			token = "Reg";
			AssertBothDeserialize(S(token)); // Regtest

			token = "Man";
			AssertBothDeserialize(S(token)); // null

			token = " ";
			AssertBothDeserialize(S(token)); // null

			token = "null";
			AssertBothDeserialize(S(token)); // null
		}

		// Failing cases.
		{
			string token = "100";
			AssertDeserializeDifferentExceptions<InvalidCastException, JsonException>(token);
		}

		// Unique cases.
		{
			string token = "null";
			AssertDeserializeFailure<ArgumentNullException>(token);
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "Network", "Network": {{jsonToken}} }""";

			TestProduct? product1 = JsonConvertOld.DeserializeObject<TestProduct>(json);
			TestProduct? product2 = JsonConvertNew.Deserialize<TestProduct>(json);

			// Value equality.
			Assert.Equal(product1, product2);
		}

		static void AssertDeserializeFailure<TException>(string jsonToken)
			where TException : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Network": {{jsonToken}} }""";

			Assert.Throws<TException>(() => JsonConvertOld.DeserializeObject<TestProduct>(json));
			Assert.Throws<TException>(() => JsonConvertNew.Deserialize<TestProduct>(json));
		}

		static void AssertDeserializeDifferentExceptions<TExceptionOld, TExceptionNew>(string jsonToken)
			where TExceptionOld : Exception
			where TExceptionNew : Exception
		{
			string json = $$"""{"Name": "Little Book of Calm", "Network": {{jsonToken}} }""";

			Assert.Throws<TExceptionOld>(() => JsonConvertOld.DeserializeObject<TestProduct>(json));
			Assert.Throws<TExceptionNew>(() => JsonConvertNew.Deserialize<TestProduct>(json));
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

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Network))]
		[Newtonsoft.Json.JsonConverter(typeof(NetworkJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(NetworkJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Network))]
		public Network? Network { get; init; }
	}

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
