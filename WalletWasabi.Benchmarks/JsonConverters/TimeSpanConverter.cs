using BenchmarkDotNet.Attributes;
using WalletWasabi.JsonConverters.Timing;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Benchmarks.JsonConverters;

[MemoryDiagnoser]
public class TimeSpanConverter
{
	private TestData testObject;

	private string json =
		"""{"DefaultTime":"0d 0h 0m 0s","OneHour":"0d 1h 0m 0s","OneDay":"1d 0h 0m 0s","MaxValue":"10675199d 2h 48m 5s","MinValue":"-10675199d -2h -48m -5s","None":null,"NotAnnotated":"00:00:00"}""";

	public TimeSpanConverter()
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

	/// <summary>
	/// Record with various attributes for both STJ and Newtonsoft.
	/// </summary>
	public record TestData
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
