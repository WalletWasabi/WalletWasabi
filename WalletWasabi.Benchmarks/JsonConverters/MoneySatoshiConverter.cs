using BenchmarkDotNet.Attributes;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Benchmarks.JsonConverters;

[MemoryDiagnoser]
public class MoneySatoshiConverter
{
	private TestData testObject;

	private string json =
		"""{"OneCoin":100000000,"OneSatoshi":1,"Zeros":100,"Max":2099999997690000,"None":null,"NotAnnotated":null}""";

	public MoneySatoshiConverter()
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

	private record TestProduct
	{
		public required string Name { get; init; }

		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Price))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneySatoshiJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneySatoshiJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Price))]
		public Money? Price { get; init; } = Money.Coins(1m);
	}
}
