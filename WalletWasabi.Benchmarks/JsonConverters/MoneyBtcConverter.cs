using BenchmarkDotNet.Attributes;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Benchmarks.JsonConverters;

[MemoryDiagnoser]
public class MoneyBtcConverter
{

	private TestData testObject;
	private string json =
		"""{"Half":"0.50","One":"1.00","Zeros":"0.000001","Max":"20999999.9769","None":null,"NotAnnotated":null}""";

	public MoneyBtcConverter()
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

	public record TestData
	{
		//Money
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
