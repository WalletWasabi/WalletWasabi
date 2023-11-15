using System.Net;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

/// <summary>
/// Tests for <see cref="EndPointJsonConverter"/> and <see cref="EndPointJsonConverterNg"/> classes.
/// </summary>
public class EndPointJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *serialize* objects equally.
	/// </summary>
	[Fact]
	public void SerializationParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"DefaultMainNet":"127.0.0.1:8333","DefaultTestNet":"127.0.0.1:18333","DefaultRegTest":"127.0.0.1:18443","None":null,"NotAnnotated":null}""", json);
	}

	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on <c>Newtonsoft.Json</c> *deserialize* objects equally.
	/// </summary>
	[Fact]
	public void DeserializationParity()
	{
		// Success cases.
		{
			string token = "null";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "0";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "0:0";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "0.0.0.0:0";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "127.0.0.1";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "127.0.0.1:0";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "127.0.0:8888";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "127.0.0.1:8333";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "255.255.255.255:8888";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "256.0.0.0:8888";
			AssertBothDeserialize(ConvertToJsonString(token));

			token = "localhost:8888";
			AssertBothDeserialize(ConvertToJsonString(token));
		}

		// Failing cases.
		{
			string invalidToken = "127.0.0.1:8888888";
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));

			invalidToken = "";
			AssertDeserializeFailure<FormatException>(ConvertToJsonString(invalidToken));
		}

		// Unique cases.
		{
			string invalidToken = "null";
			AssertDeserializeFailure<FormatException>(invalidToken);
		}

		static void AssertBothDeserialize(string jsonToken)
		{
			string json = $$"""{"Name": "IpAddress", "Address": {{jsonToken}} }""";

			TestRecord? record1 = JsonConvertOld.DeserializeObject<TestRecord>(json);
			TestRecord? record2 = JsonConvertNew.Deserialize<TestRecord>(json);

			// Value equality.
			Assert.Equal(record1, record2);
		}

		static void AssertDeserializeFailure<TException>(string jsonToken)
			where TException : Exception
		{
			string json = $$"""{"Name": "IpAddress", "Address": {{jsonToken}} }""";

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
	/// Record for testing deserialization of <see cref="EndPoint"/>.
	/// </summary>
	private record TestRecord
	{
		public required string Name { get; set; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Address))]
		[Newtonsoft.Json.JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
		[System.Text.Json.Serialization.JsonConverter(typeof(MainNetBitcoinP2pEndPointConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Address))]
		public EndPoint? Address { get; set; }
	}

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	private record TestData
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(DefaultMainNet))]
		[Newtonsoft.Json.JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
		[System.Text.Json.Serialization.JsonConverter(typeof(MainNetBitcoinP2pEndPointConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(DefaultMainNet))]
		public EndPoint DefaultMainNet { get; set; } = new IPEndPoint(IPAddress.Loopback, 0);

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(DefaultTestNet))]
		[Newtonsoft.Json.JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultTestNetBitcoinP2pPort)]
		[System.Text.Json.Serialization.JsonConverter(typeof(TestNetBitcoinP2pEndPointConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(DefaultTestNet))]
		public EndPoint DefaultTestNet { get; set; } = new IPEndPoint(IPAddress.Loopback, 0);

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(DefaultRegTest))]
		[Newtonsoft.Json.JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultRegTestBitcoinCoreRpcPort)]
		[System.Text.Json.Serialization.JsonConverter(typeof(RegTestBitcoinP2pEndPointConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(DefaultRegTest))]
		public EndPoint DefaultRegTest { get; set; } = new IPEndPoint(IPAddress.Loopback, 0);

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(None))]
		[Newtonsoft.Json.JsonConverter(typeof(EndPointJsonConverter), Constants.DefaultMainNetBitcoinP2pPort)]
		[System.Text.Json.Serialization.JsonConverter(typeof(MainNetBitcoinP2pEndPointConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(None))]
		public EndPoint? None { get; set; }

		public EndPoint? NotAnnotated { get; set; }
	}
}
