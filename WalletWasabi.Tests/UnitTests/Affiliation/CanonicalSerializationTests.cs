using Newtonsoft.Json;
using WalletWasabi.Affiliation.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class CanonicalSerializationTests
{
	[Fact]
	public void DelimitersSerialization()
	{
		var data = new { a = 1 };
		string json = JsonConvert.SerializeObject(data, CanonicalJsonSerializationOptions.Settings);
		Assert.Equal("""{"a":1}""", json);
	}

	[Fact]
	public void OrderSerialization()
	{
		string json = JsonConvert.SerializeObject(new OrderSerializationJsonObject(), CanonicalJsonSerializationOptions.Settings);
		Assert.Equal("""{"1":0,"_":0,"a":0,"b":0}""", json);
	}

	[Fact]
	public void IllegalCharacterSerialization()
	{
		Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(new IllegalCharacterSerializationJsonObject(), CanonicalJsonSerializationOptions.Settings));
	}

	private record OrderSerializationJsonObject
	{
		[JsonProperty("a")]
		public int A { get; } = 0;

		[JsonProperty("b")]
		public int B { get; } = 0;

		[JsonProperty("_")]
		public int Underscore { get; } = 0;

		[JsonProperty("1")]
		public int One { get; } = 0;
	}

	private record IllegalCharacterSerializationJsonObject
	{
		[JsonProperty("A")]
		public int CapitalA { get; } = 0;
	}
}
