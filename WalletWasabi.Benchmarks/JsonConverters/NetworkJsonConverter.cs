using BenchmarkDotNet.Attributes;
using NBitcoin;
using WalletWasabi.JsonConverters;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Benchmarks.JsonConverters;

[MemoryDiagnoser]
public class NetworkJsonConverter
{
	private TestData testObject;

	private string json =
		"""{"Main":"Main","Test":"TestNet","RegTest":"RegTest","Default":null,"NotAnnotated":null}""";

	public NetworkJsonConverter()
	{
		testObject = new TestData();
	}

	[Benchmark]
	public string OldConverterSerialize() => JsonConvertOld.SerializeObject(testObject);

	[Benchmark]
	public string NewConverterSerialize() => JsonConvertNew.Serialize(testObject);

	[Benchmark]
	public TestData? OldConverterDeserialize() => JsonConvertOld.DeserializeObject<TestData>(json);

	[Benchmark]
	public TestData? NewConverterDeserialize() => JsonConvertNew.Deserialize<TestData>(json);

	public record TestProduct
	{
		public required string Name { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Network))]
		[Newtonsoft.Json.JsonConverter(typeof(NetworkJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(NetworkJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Network))]
		public Network? Network { get; init; }
	}

	public record TestData
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
