using BenchmarkDotNet.Attributes;
using NBitcoin;
using WalletWasabi.JsonConverters;
using WalletWasabi.Tests.Helpers;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;



namespace WalletWasabi.Benchmarks.JsonConverters;

[MemoryDiagnoser]
public class ExtPubKeyConverter
{

	private string _json1;
	private string _json2;
	private TestDataSmall _data1;
	private TestDataBig _data2;

	[GlobalSetup]
	public void SetUp()
	{
		var km = ServiceFactory.CreateKeyManager(isTaprootAllowed: true);
		var segwit = km.SegwitExtPubKey.ToString(Network.Main);
		var taproot = km.TaprootExtPubKey!.ToString(Network.Main);

		_json1 = $$"""{"SegwitExtPubKey":"{{segwit}}","TaprootExtPubKey":"{{taproot}}","JustNull":null}""";
		_json2 =
			$$"""{"SegwitExtPubKey1":"{{segwit}}","TaprootExtPubKey1":"{{taproot}}","SegwitExtPubKey2":"{{segwit}}","TaprootExtPubKey2":"{{taproot}}","SegwitExtPubKey3":"{{segwit}}","TaprootExtPubKey3":"{{taproot}}","SegwitExtPubKey4":"{{segwit}}","TaprootExtPubKey4":"{{taproot}}"}""";

		_data1 = new TestDataSmall()
		{
			SegwitExtPubKey = km.SegwitExtPubKey,
			TaprootExtPubKey = km.TaprootExtPubKey
		};

		_data2 = new TestDataBig()
		{
			SegwitExtPubKey1 = km.SegwitExtPubKey,
			SegwitExtPubKey2 = km.SegwitExtPubKey,
			SegwitExtPubKey3 = km.SegwitExtPubKey,
			SegwitExtPubKey4 = km.SegwitExtPubKey,
			TaprootExtPubKey1 = km.TaprootExtPubKey,
			TaprootExtPubKey2 = km.TaprootExtPubKey,
			TaprootExtPubKey3 = km.TaprootExtPubKey,
			TaprootExtPubKey4 = km.TaprootExtPubKey
		};
	}

	[Benchmark]
	public TestDataBig? TestMicrosoftDeserializeBig() => JsonConvertNew.Deserialize<TestDataBig>(_json2);

	[Benchmark]
	public TestDataBig? TestNewtonsoftDeserializeBig() => JsonConvertOld.DeserializeObject<TestDataBig>(_json2);

	[Benchmark]
	public TestDataSmall? TestMicrosoftDeserializeSmall() => JsonConvertNew.Deserialize<TestDataSmall>(_json1);

	[Benchmark]
	public TestDataSmall? TestNewtonsoftDeserializeSmall() => JsonConvertOld.DeserializeObject<TestDataSmall>(_json1);

	[Benchmark]
	public string TestMicrosoftSerializeBig() => JsonConvertNew.Serialize(_data2);

	[Benchmark]
	public string TestNewtonsoftSerializeBig() => JsonConvertOld.SerializeObject(_data2);

	[Benchmark]
	public string TestMicrosoftSerializeSmall() => JsonConvertNew.Serialize(_data1);

	[Benchmark]
	public string? TestNewtonsoftSerializeSmall() => JsonConvertOld.SerializeObject(_data1);


	public record TestDataSmall
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SegwitExtPubKey))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SegwitExtPubKey))]
		public ExtPubKey? SegwitExtPubKey { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(TaprootExtPubKey))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(TaprootExtPubKey))]
		public ExtPubKey? TaprootExtPubKey { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(JustNull))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(JustNull))]
		public ExtPubKey? JustNull { get; init; }
	}

	/// <summary>
	/// Record for testing deserialization of <see cref="ExtPubKey"/>.
	/// </summary>
	public record TestDataBig
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SegwitExtPubKey1))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SegwitExtPubKey1))]
		public ExtPubKey? SegwitExtPubKey1 { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(TaprootExtPubKey1))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(TaprootExtPubKey1))]
		public ExtPubKey? TaprootExtPubKey1 { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SegwitExtPubKey2))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SegwitExtPubKey2))]
		public ExtPubKey? SegwitExtPubKey2 { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(TaprootExtPubKey2))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(TaprootExtPubKey2))]
		public ExtPubKey? TaprootExtPubKey2 { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SegwitExtPubKey3))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SegwitExtPubKey3))]
		public ExtPubKey? SegwitExtPubKey3 { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(TaprootExtPubKey3))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(TaprootExtPubKey3))]
		public ExtPubKey? TaprootExtPubKey3 { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SegwitExtPubKey4))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(SegwitExtPubKey4))]
		public ExtPubKey? SegwitExtPubKey4 { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(TaprootExtPubKey4))]
		[Newtonsoft.Json.JsonConverter(typeof(ExtPubKeyJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(ExtPubKeyJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(TaprootExtPubKey4))]
		public ExtPubKey? TaprootExtPubKey4 { get; init; }
	}
}


